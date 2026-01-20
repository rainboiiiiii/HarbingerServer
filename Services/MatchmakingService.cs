using GameBackend.Api.Data;
using GameBackend.Api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GameBackend.Api.Services;

public class MatchmakingService
{
    private readonly MongoDbContext _db;
    private readonly MatchmakingOptions _options;
    private readonly ILogger<MatchmakingService> _logger;

    public MatchmakingService(MongoDbContext db, IOptions<MatchmakingOptions> options, ILogger<MatchmakingService> logger)
    {
        _db = db;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<(QueueTicket ticket, bool conflict)> EnqueueAsync(string userId, string mode, string region, int? playersPerMatch, CancellationToken ct = default)
    {
        var playersNeeded = playersPerMatch ?? _options.DefaultPlayersPerMatch;
        var existing = await _db.QueueTickets
            .Find(q => q.UserId == userId && q.Mode == mode && q.Region == region && q.State == "queued")
            .FirstOrDefaultAsync(ct);
        if (existing != null)
        {
            return (existing, true);
        }

        var ticket = new QueueTicket
        {
            UserId = userId,
            Mode = mode,
            Region = region,
            PlayersPerMatch = playersNeeded,
            EnqueuedAt = DateTime.UtcNow,
            State = "queued"
        };

        try
        {
            await _db.QueueTickets.InsertOneAsync(ticket, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var dup = await _db.QueueTickets
                .Find(q => q.UserId == userId && q.Mode == mode && q.Region == region && q.State == "queued")
                .FirstOrDefaultAsync(ct);
            return (dup ?? ticket, true);
        }

        await TryFormMatchAsync(mode, region, playersNeeded, ct);
        return (ticket, false);
    }

    public async Task<bool> CancelAsync(string userId, CancellationToken ct = default)
    {
        var filter = Builders<QueueTicket>.Filter.Eq(q => q.UserId, userId) &
                     Builders<QueueTicket>.Filter.Eq(q => q.State, "queued");

        var update = Builders<QueueTicket>.Update.Set(q => q.State, "canceled");
        var result = await _db.QueueTickets.FindOneAndUpdateAsync(filter,
            update,
            new FindOneAndUpdateOptions<QueueTicket>
            {
                Sort = Builders<QueueTicket>.Sort.Descending(q => q.EnqueuedAt),
                ReturnDocument = ReturnDocument.After
            }, ct);

        return result != null;
    }

    public async Task<MatchStatusResponse> GetStatusAsync(string userId, CancellationToken ct = default)
    {
        var ticket = await _db.QueueTickets
            .Find(q => q.UserId == userId && (q.State == "queued" || q.State == "matched"))
            .SortByDescending(q => q.EnqueuedAt)
            .FirstOrDefaultAsync(ct);

        if (ticket == null)
        {
            return new MatchStatusResponse { Status = "idle" };
        }

        if (ticket.State == "queued")
        {
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

        if (ticket.State == "matched" && ticket.MatchId != null)
        {
            var match = await GetMatchAsync(ticket.MatchId, ct);
            if (match != null)
            {
                return new MatchStatusResponse
                {
                    Status = "matched",
                    Match = match
                };
            }
        }

        return new MatchStatusResponse { Status = "idle" };
    }

    public async Task<MatchInfo?> GetMatchAsync(string matchId, CancellationToken ct = default)
    {
        var match = await _db.Matches.Find(m => m.Id == matchId).FirstOrDefaultAsync(ct);
        if (match == null)
        {
            return null;
        }

        return new MatchInfo
        {
            MatchId = match.Id,
            Mode = match.Mode,
            Region = match.Region,
            Players = match.Players,
            CreatedAt = match.CreatedAt,
            State = match.State
        };
    }

    private async Task TryFormMatchAsync(string mode, string region, int playersPerMatch, CancellationToken ct)
    {
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
                await session.CommitTransactionAsync(ct);
                return;
            }

            var matchId = Guid.NewGuid().ToString();
            var match = new Match
            {
                Id = matchId,
                Mode = mode,
                Region = region,
                State = "matched",
                CreatedAt = DateTime.UtcNow,
                Players = candidates.Select(c => c.UserId).ToList()
            };

            await _db.Matches.InsertOneAsync(session, match, cancellationToken: ct);

            var updateFilter = Builders<QueueTicket>.Filter.In(q => q.Id, candidates.Select(c => c.Id)) &
                               Builders<QueueTicket>.Filter.Eq(q => q.State, "queued");

            var update = Builders<QueueTicket>.Update
                .Set(q => q.State, "matched")
                .Set(q => q.MatchId, matchId);

            await _db.QueueTickets.UpdateManyAsync(session, updateFilter, update, cancellationToken: ct);
            await session.CommitTransactionAsync(ct);
        }
        catch (MongoCommandException ex)
        {
            _logger.LogWarning(ex, "MongoDB transaction failed, attempting best-effort match");
            await TryFormMatchWithoutTransactionAsync(mode, region, playersPerMatch, ct);
        }
        catch (NotSupportedException)
        {
            await TryFormMatchWithoutTransactionAsync(mode, region, playersPerMatch, ct);
        }
    }

    private async Task TryFormMatchWithoutTransactionAsync(string mode, string region, int playersPerMatch, CancellationToken ct)
    {
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
            var match = new Match
            {
                Id = matchId,
                Mode = mode,
                Region = region,
                State = "matched",
                CreatedAt = DateTime.UtcNow,
                Players = candidates.Select(c => c.UserId).ToList()
            };
            await _db.Matches.InsertOneAsync(match, cancellationToken: ct);
        }
    }
}
