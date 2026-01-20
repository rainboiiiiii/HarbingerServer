using GameBackend.Api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GameBackend.Api.Data;

public class MongoDbContext
{
    private readonly ILogger<MongoDbContext> _logger;
    private readonly MongoClient _client;
    private readonly IMongoDatabase _database;

    public MongoDbContext(IOptions<MongoOptions> options, ILogger<MongoDbContext> logger)
    {
        _logger = logger;
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ConnectionString) || string.IsNullOrWhiteSpace(settings.DatabaseName))
        {
            throw new InvalidOperationException("MongoDB configuration is missing ConnectionString or DatabaseName");
        }
        _client = new MongoClient(settings.ConnectionString);
        _database = _client.GetDatabase(settings.DatabaseName);
    }

    public MongoClient Client => _client;

    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<UserProgression> UserProgressions => _database.GetCollection<UserProgression>("user_progression");
    public IMongoCollection<UserBattlePassClaim> UserBattlePassClaims => _database.GetCollection<UserBattlePassClaim>("user_battlepass_claims");
    public IMongoCollection<QueueTicket> QueueTickets => _database.GetCollection<QueueTicket>("queue_tickets");
    public IMongoCollection<Match> Matches => _database.GetCollection<Match>("matches");

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ensuring MongoDB indexes");

        var userIndex = new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(u => u.UsernameNormalized),
            new CreateIndexOptions { Unique = true, Name = "idx_users_usernameNormalized_unique" });
        await Users.Indexes.CreateOneAsync(userIndex, cancellationToken: cancellationToken);

        var claimIndex = new CreateIndexModel<UserBattlePassClaim>(
            Builders<UserBattlePassClaim>.IndexKeys
                .Ascending(c => c.UserId)
                .Ascending(c => c.Season)
                .Ascending(c => c.TierIndex)
                .Ascending(c => c.IsPremium),
            new CreateIndexOptions { Unique = true, Name = "idx_claim_unique" });
        await UserBattlePassClaims.Indexes.CreateOneAsync(claimIndex, cancellationToken: cancellationToken);

        var queueLookupIndex = new CreateIndexModel<QueueTicket>(
            Builders<QueueTicket>.IndexKeys
                .Ascending(q => q.Mode)
                .Ascending(q => q.Region)
                .Ascending(q => q.PlayersPerMatch)
                .Ascending(q => q.State)
                .Ascending(q => q.EnqueuedAt),
            new CreateIndexOptions { Name = "idx_queue_matchmaking" });
        await QueueTickets.Indexes.CreateOneAsync(queueLookupIndex, cancellationToken: cancellationToken);

        var queueUniqueQueuedIndex = new CreateIndexModel<QueueTicket>(
            Builders<QueueTicket>.IndexKeys
                .Ascending(q => q.UserId)
                .Ascending(q => q.Mode)
                .Ascending(q => q.Region)
                .Ascending(q => q.State),
            new CreateIndexOptions<QueueTicket>
            {
                Name = "idx_queue_user_unique_when_queued",
                Unique = true,
                PartialFilterExpression = Builders<QueueTicket>.Filter.Eq(q => q.State, "queued")
            });
        await QueueTickets.Indexes.CreateOneAsync(queueUniqueQueuedIndex, cancellationToken: cancellationToken);

        var matchIndex = new CreateIndexModel<Match>(
            Builders<Match>.IndexKeys.Descending(m => m.CreatedAt),
            new CreateIndexOptions { Name = "idx_match_createdAt" });
        await Matches.Indexes.CreateOneAsync(matchIndex, cancellationToken: cancellationToken);
    }
}
