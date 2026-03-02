using GameBackend.Api.Data;
using GameBackend.Api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;

namespace GameBackend.Api.Services;

public class MatchmakingService
{
    private readonly MongoDbContext _db;
    private readonly MatchmakingOptions _options;
    private readonly ILogger<MatchmakingService> _logger;
    private readonly GameServerService _gameServerService;
    private readonly UnityMatchmakingService _unityMatchmakingService;

    public MatchmakingService(MongoDbContext db, IOptions<MatchmakingOptions> options, ILogger<MatchmakingService> logger, GameServerService gameServerService, UnityMatchmakingService unityMatchmakingService)
    {
        _db = db;
        _logger = logger;
        _options = options.Value;
        _gameServerService = gameServerService;
        _unityMatchmakingService = unityMatchmakingService;
    }

    public async Task<(QueueTicket ticket, bool conflict)> EnqueueAsync(string userId, string mode, string region, int? playersPerMatch, List<string> equippedWeapons, List<string> equippedAbilities, CancellationToken ct = default)
    {
        _logger.LogInformation("User {UserId} enqueuing for match in mode {Mode}, region {Region} with {PlayersPerMatch} players.", userId, mode, region, playersPerMatch);
        var playersNeeded = playersPerMatch ?? _options.DefaultPlayersPerMatch;

        var existing = await _db.QueueTickets
            .Find(q => q.UserId == userId && q.Mode == mode && q.Region == region && q.State == "queued")
            .FirstOrDefaultAsync(ct);

        if (existing != null)
        {
            _logger.LogInformation("User {UserId} is already queued for mode {Mode}, region {Region}.", userId, mode, region);
            return (existing, true);
        }

        // 1. Define unityPlayers BEFORE the try block
        // 1. Define unityPlayers
        var unityPlayers = new List<UnityMatchmakingPlayer>
{
    new UnityMatchmakingPlayer { Id = userId }
};

        // 2. Start with an EMPTY dictionary to test the queue connection
        // If this succeeds, you need to add "mode" and "region" to your 
        // Matchmaker Config in the Unity Dashboard before sending them here.
        var unityAttributes = new Dictionary<string, object>();

        string unityTicketId;
        try
        {
            // Matches your image_5ec500.png exactly
            var queueName = "OuterEdge";

            unityTicketId = await _unityMatchmakingService.CreateTicketAsync(queueName, unityAttributes, unityPlayers);
            _logger.LogInformation("Unity matchmaking ticket {UnityTicketId} created successfully!", unityTicketId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Unity matchmaking ticket.");
            throw;
        }

        var ticket = new QueueTicket
        {
            UserId = userId,
            Mode = mode,
            Region = region,
            PlayersPerMatch = playersNeeded,
            EnqueuedAt = DateTime.UtcNow,
            State = "queued", // Still mark as queued locally until Unity reports a match
            UnityTicketId = unityTicketId, // Store the Unity ticket ID
            EquippedWeapons = equippedWeapons ?? new List<string>(),
            EquippedAbilities = equippedAbilities ?? new List<string>()
        };

        try
        {
            await _db.QueueTickets.InsertOneAsync(ticket, cancellationToken: ct);
            _logger.LogInformation("Local queue ticket {TicketId} saved for user {UserId} with Unity Ticket {UnityTicketId}.", ticket.Id, userId, unityTicketId);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            _logger.LogWarning(ex, "Duplicate key error when saving local queue ticket for user {UserId}. This should not happen if UnityTicketId is unique.", userId);
            var dup = await _db.QueueTickets
                .Find(q => q.UserId == userId && q.Mode == mode && q.Region == region && q.State == "queued")
                .FirstOrDefaultAsync(ct);
            // If a duplicate is found, try to cancel the Unity ticket that was just created
            if (dup != null && dup.UnityTicketId != null)
            {
                _logger.LogInformation("Attempting to cancel newly created Unity ticket {UnityTicketId} due to local duplicate.", unityTicketId);
                try
                {
                    await _unityMatchmakingService.DeleteTicketAsync(unityTicketId);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogError(deleteEx, "Failed to cancel Unity ticket {UnityTicketId} after local duplicate detected.", unityTicketId);
                }
            }
            return (dup ?? ticket, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save local queue ticket for user {UserId}.", userId);
            throw;
        }

        // Remove the local match forming logic, Unity Matchmaking will handle this
        // await TryFormMatchAsync(mode, region, playersNeeded, ct);
        return (ticket, false);
    }

    public async Task<bool> CancelAsync(string userId, CancellationToken ct = default)
    {
        _logger.LogInformation("User {UserId} attempting to cancel matchmaking.", userId);

        var ticket = await _db.QueueTickets
            .Find(q => q.UserId == userId && q.State == "queued")
            .SortByDescending(q => q.EnqueuedAt)
            .FirstOrDefaultAsync(ct);

        if (ticket == null)
        {
            _logger.LogInformation("User {UserId} had no active queue ticket to cancel.", userId);
            return false;
        }

        if (!string.IsNullOrEmpty(ticket.UnityTicketId))
        {
            try
            {
                await _unityMatchmakingService.DeleteTicketAsync(ticket.UnityTicketId);
                _logger.LogInformation("Unity matchmaking ticket {UnityTicketId} canceled for user {UserId}.", ticket.UnityTicketId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel Unity matchmaking ticket {UnityTicketId} for user {UserId}.", ticket.UnityTicketId, userId);
                // Continue to update local state even if Unity cancellation fails
            }
        }

        var filter = Builders<QueueTicket>.Filter.Eq(q => q.Id, ticket.Id);
        var update = Builders<QueueTicket>.Update.Set(q => q.State, "canceled");

        var result = await _db.QueueTickets.FindOneAndUpdateAsync(filter,
            update,
            new FindOneAndUpdateOptions<QueueTicket>
            {
                ReturnDocument = ReturnDocument.After
            }, ct);

        if (result != null)
        {
            _logger.LogInformation("User {UserId} successfully canceled matchmaking (local ticket {TicketId}).", userId, ticket.Id);
            return true;
        }
        
        return false;
    }

    public async Task<MatchStatusResponse> GetStatusAsync(string userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Checking matchmaking status for user {UserId}.", userId);
        var ticket = await _db.QueueTickets
            .Find(q => q.UserId == userId && (q.State == "queued" || q.State == "matched"))
            .SortByDescending(q => q.EnqueuedAt)
            .FirstOrDefaultAsync(ct);

        if (ticket == null)
        {
            _logger.LogDebug("User {UserId} has no active queue ticket.", userId);
            return new MatchStatusResponse { Status = "idle" };
        }

        if (ticket.State == "queued" && !string.IsNullOrEmpty(ticket.UnityTicketId))
        {
            try
            {
                var unityStatus = await _unityMatchmakingService.GetTicketStatusAsync(ticket.UnityTicketId);

                // Unity's Matchmaking API returns status "Found" when a match is made and assigned
                if (unityStatus.Status == "Found" && !string.IsNullOrEmpty(unityStatus.MatchId))
                {
                    _logger.LogInformation("Unity matchmaking ticket {UnityTicketId} for user {UserId} found a match {UnityMatchId}.", ticket.UnityTicketId, userId, unityStatus.MatchId);

                    // Fetch full matchmaking results from Unity
                    var unityMatchResults = await _unityMatchmakingService.GetMatchmakingResultsAsync(unityStatus.MatchId);

                    // Provision game server
                    var map = unityMatchResults.MatchProperties.ContainsKey("map") ? unityMatchResults.MatchProperties["map"]?.ToString() ?? "Outer Edge" : _options.AvailableMaps.FirstOrDefault() ?? "Outer Edge";
                    _logger.LogInformation("Provisioning game server for Unity match {UnityMatchId} on map {Map}.", unityStatus.MatchId, map);
                    var (serverIp, serverPort) = await _gameServerService.ProvisionServerAsync(unityStatus.MatchId, map, ct);
                    _logger.LogInformation("Game server provisioned for Unity match {UnityMatchId}: {ServerIp}:{ServerPort}.", unityStatus.MatchId, serverIp, serverPort);

                    // Create our local Match record
                    var matchPlayers = unityMatchResults.MatchProperties.ContainsKey("players") 
                        ? ((Newtonsoft.Json.Linq.JArray)unityMatchResults.MatchProperties["players"]).Select(p => p.ToString()).ToList() 
                        : new List<string>();

                    var match = new Match
                    {
                        Id = unityStatus.MatchId, // Use Unity's MatchId as our local Match Id
                        Mode = unityMatchResults.MatchProperties.ContainsKey("mode") ? unityMatchResults.MatchProperties["mode"]?.ToString() ?? ticket.Mode : ticket.Mode,
                        Region = unityMatchResults.MatchProperties.ContainsKey("region") ? unityMatchResults.MatchProperties["region"]?.ToString() ?? ticket.Region : ticket.Region,
                        Map = map,
                        State = "matched",
                        CreatedAt = DateTime.UtcNow,
                        Players = matchPlayers,
                        ServerIp = serverIp,
                        ServerPort = serverPort,
                        GeneratorName = unityMatchResults.GeneratorName,
                        QueueName = unityMatchResults.QueueName,
                        PoolName = unityMatchResults.PoolName,
                        EnvironmentId = unityMatchResults.EnvironmentId,
                        BackfillTicketId = unityMatchResults.BackfillTicketId,
                        PoolId = unityMatchResults.PoolId,
                        MatchProperties = unityMatchResults.MatchProperties
                    };

                    await _db.Matches.InsertOneAsync(match, cancellationToken: ct);
                    _logger.LogInformation("Local Match {MatchId} created for Unity match.", match.Id);

                    // Update local QueueTicket to 'matched'
                    var updateFilter = Builders<QueueTicket>.Filter.Eq(q => q.Id, ticket.Id);
                    var update = Builders<QueueTicket>.Update
                        .Set(q => q.State, "matched")
                        .Set(q => q.MatchId, match.Id); // Link to our local Match Id

                    await _db.QueueTickets.UpdateOneAsync(updateFilter, update, cancellationToken: ct);
                    _logger.LogInformation("Local QueueTicket {TicketId} updated to 'matched' for Unity match {MatchId}.", ticket.Id, match.Id);

                    return new MatchStatusResponse
                    {
                        Status = "matched",
                        Match = await GetMatchAsync(match.Id, ct) // Return our local MatchInfo
                    };
                }
                else if (unityStatus.Status == "Pending" || unityStatus.Status == "Searching")
                {
                    _logger.LogDebug("User {UserId} is currently queued with Unity ticket {UnityTicketId}. Unity status: {UnityStatus}", userId, ticket.UnityTicketId, unityStatus.Status);
                    return new MatchStatusResponse
                    {
                        Status = "queued",
                        Queue = new QueueStatus
                        {
                            TicketId = ticket.Id,
                            Mode = ticket.Mode,
                            Region = ticket.Region,
                            EnqueuedAt = ticket.EnqueuedAt
                        }
                    };
                }
                else // Other Unity statuses (e.g., "NotFound", "Failed")
                {
                    _logger.LogWarning("Unity matchmaking ticket {UnityTicketId} for user {UserId} has unexpected status: {UnityStatus}. Cancelling local ticket.", ticket.UnityTicketId, userId, unityStatus.Status);
                    // Update local ticket to canceled and return idle
                    var updateFilter = Builders<QueueTicket>.Filter.Eq(q => q.Id, ticket.Id);
                    var update = Builders<QueueTicket>.Update.Set(q => q.State, "canceled");
                    await _db.QueueTickets.UpdateOneAsync(updateFilter, update, cancellationToken: ct);
                    return new MatchStatusResponse { Status = "idle" };
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to get Unity matchmaking status for ticket {UnityTicketId} for user {UserId}. Returning local queue status.", ticket.UnityTicketId, userId);
                // Fallback to local queued status if Unity API call fails
                return new MatchStatusResponse
                {
                    Status = "queued",
                    Queue = new QueueStatus
                    {
                        TicketId = ticket.Id,
                        Mode = ticket.Mode,
                        Region = ticket.Region,
                        EnqueuedAt = ticket.EnqueuedAt
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while processing Unity matchmaking status for ticket {UnityTicketId} for user {UserId}.", ticket.UnityTicketId, userId);
                // Fallback to local queued status if unexpected error
                return new MatchStatusResponse
                {
                    Status = "queued",
                    Queue = new QueueStatus
                    {
                        TicketId = ticket.Id,
                        Mode = ticket.Mode,
                        Region = ticket.Region,
                        EnqueuedAt = ticket.EnqueuedAt
                    }
                };
            }
        }
        // If local ticket is 'queued' but no UnityTicketId (legacy or error in Enqueue), or if Unity API not used
        else if (ticket.State == "queued")
        {
            _logger.LogDebug("User {UserId} is currently queued (ticket {TicketId}).", userId, ticket.Id);
            return new MatchStatusResponse
            {
                Status = "queued",
                Queue = new QueueStatus
                {
                    TicketId = ticket.Id,
                    Mode = ticket.Mode,
                    Region = ticket.Region,
                    EnqueuedAt = ticket.EnqueuedAt
                }
            };
        }
        // If local ticket is 'matched'
        else if (ticket.State == "matched" && ticket.MatchId != null)
        {
            _logger.LogDebug("User {UserId} has a matched ticket (ticket {TicketId}, match {MatchId}).", userId, ticket.Id, ticket.MatchId);
            var match = await GetMatchAsync(ticket.MatchId, ct);
            if (match != null)
            {
                _logger.LogDebug("Returning match info for user {UserId} (match {MatchId}).", userId, ticket.MatchId);
                return new MatchStatusResponse
                {
                    Status = "matched",
                    Match = match
                };
            }
            _logger.LogWarning("Match {MatchId} not found for matched ticket {TicketId} for user {UserId}. Falling back to idle.", ticket.MatchId, ticket.Id, userId);
            // If match record is somehow missing, update local ticket to canceled
            var updateFilter = Builders<QueueTicket>.Filter.Eq(q => q.Id, ticket.Id);
            var update = Builders<QueueTicket>.Update.Set(q => q.State, "canceled");
            await _db.QueueTickets.UpdateOneAsync(updateFilter, update, cancellationToken: ct);
            return new MatchStatusResponse { Status = "idle" };
        }

        _logger.LogDebug("User {UserId} status is idle by default after checks.", userId);
        return new MatchStatusResponse { Status = "idle" };
    }

    public async Task<MatchInfo?> GetMatchAsync(string matchId, CancellationToken ct = default)
    {
        _logger.LogDebug("Attempting to retrieve match {MatchId}.", matchId);
        var match = await _db.Matches.Find(m => m.Id == matchId).FirstOrDefaultAsync(ct);
        if (match == null)
        {
            _logger.LogWarning("Match {MatchId} not found.", matchId);
            return null;
        }

        _logger.LogDebug("Successfully retrieved match {MatchId}.", matchId);
        return new MatchInfo
        {
            MatchId = match.Id,
            Mode = match.Mode,
            Region = match.Region,
            Map = match.Map,
            Players = match.Players,
            CreatedAt = match.CreatedAt,
            State = match.State,
            ServerIp = match.ServerIp,
            ServerPort = match.ServerPort
        };
    }

    private async Task TryFormMatchAsync(string mode, string region, int playersPerMatch, CancellationToken ct)
    {
        _logger.LogInformation("Attempting to form match for mode {Mode}, region {Region}, players {PlayersPerMatch} (with transaction).", mode, region, playersPerMatch);
        try
        {
            using var session = await _db.Client.StartSessionAsync(cancellationToken: ct);
            session.StartTransaction();

            var candidateFilter = Builders<QueueTicket>.Filter.Eq(q => q.Mode, mode) &
                                  Builders<QueueTicket>.Filter.Eq(q => q.Region, region) &
                                  Builders<QueueTicket>.Filter.Eq(q => q.PlayersPerMatch, playersPerMatch) &
                                  Builders<QueueTicket>.Filter.Eq(q => q.State, "queued");

            var candidates = await _db.QueueTickets
                .Find(session, candidateFilter)
                .SortBy(q => q.EnqueuedAt)
                .Limit(playersPerMatch)
                .ToListAsync(ct);

            if (candidates.Count < playersPerMatch)
            {
                _logger.LogInformation("Not enough candidates ({Count}/{Required}) to form a match for mode {Mode}, region {Region}.", candidates.Count, playersPerMatch, mode, region);
                await session.CommitTransactionAsync(ct); // Commit empty transaction
                return;
            }

            var map = _options.AvailableMaps.Count > 0 
                ? _options.AvailableMaps[Random.Shared.Next(_options.AvailableMaps.Count)] 
                : "Outer Edge";

            var matchId = Guid.NewGuid().ToString();
            
            _logger.LogInformation("Provisioning game server for match {MatchId} on map {Map}.", matchId, map);
            // Provision a game server for the match
            var (serverIp, serverPort) = await _gameServerService.ProvisionServerAsync(matchId, map, ct);
            _logger.LogInformation("Game server provisioned for match {MatchId}: {ServerIp}:{ServerPort}.", matchId, serverIp, serverPort);

            var match = new Match
            {
                Id = matchId,
                Mode = mode,
                Region = region,
                Map = map,
                State = "matched",
                CreatedAt = DateTime.UtcNow,
                Players = candidates.Select(c => c.UserId).ToList(),
                ServerIp = serverIp,
                ServerPort = serverPort
            };

            await _db.Matches.InsertOneAsync(session, match, cancellationToken: ct);
            _logger.LogInformation("Match {MatchId} created with players {@Players} on {ServerIp}:{ServerPort}.", match.Id, match.Players, match.ServerIp, match.ServerPort);

            var updateFilter = Builders<QueueTicket>.Filter.In(q => q.Id, candidates.Select(c => c.Id)) &
                               Builders<QueueTicket>.Filter.Eq(q => q.State, "queued");

            var update = Builders<QueueTicket>.Update
                .Set(q => q.State, "matched")
                .Set(q => q.MatchId, matchId);

            await _db.QueueTickets.UpdateManyAsync(session, updateFilter, update, cancellationToken: ct);
            _logger.LogInformation("Updated {Count} queue tickets to 'matched' for match {MatchId}.", candidates.Count, matchId);
            await session.CommitTransactionAsync(ct);
            _logger.LogInformation("Transaction committed for match {MatchId}.", matchId);
        }
        catch (MongoCommandException ex)
        {
            _logger.LogWarning(ex, "MongoDB transaction failed for mode {Mode}, region {Region}. Attempting best-effort match without transaction.", mode, region);
            await TryFormMatchWithoutTransactionAsync(mode, region, playersPerMatch, ct);
        }
        catch (NotSupportedException)
        {
            _logger.LogWarning("Transactions not supported for mode {Mode}, region {Region}. Attempting best-effort match without transaction.", mode, region);
            await TryFormMatchWithoutTransactionAsync(mode, region, playersPerMatch, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while trying to form a match for mode {Mode}, region {Region}.", mode, region);
            throw;
        }
    }

    private async Task TryFormMatchWithoutTransactionAsync(string mode, string region, int playersPerMatch, CancellationToken ct)
    {
        _logger.LogInformation("Attempting to form match for mode {Mode}, region {Region}, players {PlayersPerMatch} (without transaction).", mode, region, playersPerMatch);
        var filter = Builders<QueueTicket>.Filter.Eq(q => q.Mode, mode) &
                     Builders<QueueTicket>.Filter.Eq(q => q.Region, region) &
                     Builders<QueueTicket>.Filter.Eq(q => q.PlayersPerMatch, playersPerMatch) &
                     Builders<QueueTicket>.Filter.Eq(q => q.State, "queued");

        var candidates = await _db.QueueTickets
            .Find(filter)
            .SortBy(q => q.EnqueuedAt)
            .Limit(playersPerMatch)
            .ToListAsync(ct);

        if (candidates.Count < playersPerMatch)
        {
            _logger.LogInformation("Not enough candidates ({Count}/{Required}) to form a match (without transaction) for mode {Mode}, region {Region}.", candidates.Count, playersPerMatch, mode, region);
            return;
        }

        var ids = candidates.Select(c => c.Id).ToArray();
        var atomicFilter = Builders<QueueTicket>.Filter.In(q => q.Id, ids) &
                           Builders<QueueTicket>.Filter.Eq(q => q.State, "queued");

        var matchId = Guid.NewGuid().ToString();
        var update = Builders<QueueTicket>.Update.Set(q => q.State, "matched").Set(q => q.MatchId, matchId);

        var updateResult = await _db.QueueTickets.UpdateManyAsync(atomicFilter, update, cancellationToken: ct);
        if (updateResult.ModifiedCount == playersPerMatch)
        {
            _logger.LogInformation("Successfully updated {Count} queue tickets to 'matched' for match {MatchId} (without transaction).", playersPerMatch, matchId);
            var map = _options.AvailableMaps.Count > 0 
                ? _options.AvailableMaps[Random.Shared.Next(_options.AvailableMaps.Count)] 
                : "Outer Edge";

            _logger.LogInformation("Provisioning game server for match {MatchId} on map {Map} (without transaction).", matchId, map);
            // Provision a game server for the match
            var (serverIp, serverPort) = await _gameServerService.ProvisionServerAsync(matchId, map, ct);
            _logger.LogInformation("Game server provisioned for match {MatchId}: {ServerIp}:{ServerPort} (without transaction).", matchId, serverIp, serverPort);

            var match = new Match
            {
                Id = matchId,
                Mode = mode,
                Region = region,
                Map = map,
                State = "matched",
                CreatedAt = DateTime.UtcNow,
                Players = candidates.Select(c => c.UserId).ToList(),
                ServerIp = serverIp,
                ServerPort = serverPort
            };
            await _db.Matches.InsertOneAsync(match, cancellationToken: ct);
            _logger.LogInformation("Match {MatchId} created with players {@Players} on {ServerIp}:{ServerPort} (without transaction).", match.Id, match.Players, match.ServerIp, match.ServerPort);
        }
        else
        {
            _logger.LogWarning("Failed to update all required queue tickets to 'matched' (expected {Expected}, got {Actual}) for match {MatchId} (without transaction).", playersPerMatch, updateResult.ModifiedCount, matchId);
        }
    }
}
