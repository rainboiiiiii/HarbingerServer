using GameBackend.Api.Data;
using GameBackend.Api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GameBackend.Api.Services;

public class ProgressionService
{
    private readonly MongoDbContext _db;
    private readonly ProgressionOptions _progressionOptions;
    private readonly BattlePassOptions _battlePassOptions;
    private readonly ILogger<ProgressionService> _logger;

    public ProgressionService(
        MongoDbContext db,
        IOptions<ProgressionOptions> progressionOptions,
        IOptions<BattlePassOptions> battlePassOptions,
        ILogger<ProgressionService> logger)
    {
        _db = db;
        _progressionOptions = progressionOptions.Value;
        _battlePassOptions = battlePassOptions.Value;
        _logger = logger;
    }

    public async Task<ProgressionResponse> GetProgressionAsync(string userId, CancellationToken ct = default)
    {
        var progression = await EnsureProgressionAsync(userId, ct);
        return await BuildResponseAsync(userId, progression, ct);
    }

    public async Task<ProgressionResponse> AddXpAsync(string userId, long xpToAdd, CancellationToken ct = default)
    {
        var progression = await EnsureProgressionAsync(userId, ct);
        progression.Xp += xpToAdd;
        progression.Level = CalculateLevel(progression.Xp);
        await _db.UserProgressions.ReplaceOneAsync(
            p => p.Id == userId,
            progression,
            new ReplaceOptions { IsUpsert = true },
            ct);

        return await BuildResponseAsync(userId, progression, ct);
    }

    public async Task<ProgressionResponse> AddRewardsAsync(string userId, long xpToAdd, int dustToAdd, int crystalsToAdd, CancellationToken ct = default)
    {
        var progression = await EnsureProgressionAsync(userId, ct);
        progression.Xp += xpToAdd;
        progression.Level = CalculateLevel(progression.Xp);
        progression.Dust += dustToAdd;
        progression.Crystals += crystalsToAdd;

        await _db.UserProgressions.ReplaceOneAsync(
            p => p.Id == userId,
            progression,
            new ReplaceOptions { IsUpsert = true },
            ct);

        return await BuildResponseAsync(userId, progression, ct);
    }

    public async Task<ProgressionResponse> PurchaseItemAsync(string userId, string itemId, string currencyType, int cost, CancellationToken ct = default)
    {
        var progression = await EnsureProgressionAsync(userId, ct);

        // If already owned, don't charge, just return current state
        if (progression.Inventory.Contains(itemId))
        {
            return await BuildResponseAsync(userId, progression, ct);
        }

        if (string.Equals(currencyType, "Dust", StringComparison.OrdinalIgnoreCase))
        {
            if (progression.Dust < cost)
            {
                throw new InvalidOperationException("Not enough dust");
            }
            progression.Dust -= cost;
        }
        else if (string.Equals(currencyType, "Crystals", StringComparison.OrdinalIgnoreCase))
        {
            if (progression.Crystals < cost)
            {
                throw new InvalidOperationException("Not enough crystals");
            }
            progression.Crystals -= cost;
        }
        else
        {
            throw new ArgumentException($"Invalid currency type: {currencyType}");
        }

        progression.Inventory.Add(itemId);

        await _db.UserProgressions.ReplaceOneAsync(
            p => p.Id == userId,
            progression,
            new ReplaceOptions { IsUpsert = true },
            ct);

        return await BuildResponseAsync(userId, progression, ct);
    }

    public async Task<ProgressionResponse> UnlockBattlePassAsync(string userId, CancellationToken ct = default)
    {
        var progression = await EnsureProgressionAsync(userId, ct);
        progression.HasPremiumPass = true;
        await _db.UserProgressions.ReplaceOneAsync(p => p.Id == userId, progression, new ReplaceOptions { IsUpsert = true }, ct);
        return await BuildResponseAsync(userId, progression, ct);
    }

    public async Task<(ProgressionResponse response, bool duplicate)> ClaimTierAsync(string userId, int tierIndex, bool isPremium, CancellationToken ct = default)
    {
        var progression = await EnsureProgressionAsync(userId, ct);
        if (isPremium && !progression.HasPremiumPass)
        {
            throw new InvalidOperationException("Premium pass not unlocked");
        }

        var claim = new UserBattlePassClaim
        {
            UserId = userId,
            Season = _battlePassOptions.CurrentSeason,
            TierIndex = tierIndex,
            IsPremium = isPremium,
            ClaimedAt = DateTime.UtcNow
        };

        try
        {
            await _db.UserBattlePassClaims.InsertOneAsync(claim, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            _logger.LogWarning("Duplicate claim for user {UserId} season {Season} tier {Tier} premium {Premium}", userId, _battlePassOptions.CurrentSeason, tierIndex, isPremium);
            return (await BuildResponseAsync(userId, progression, ct), true);
        }

        return (await BuildResponseAsync(userId, progression, ct), false);
    }

    private async Task<ProgressionResponse> BuildResponseAsync(string userId, UserProgression progression, CancellationToken ct)
    {
        var claims = await _db.UserBattlePassClaims
            .Find(c => c.UserId == userId && c.Season == _battlePassOptions.CurrentSeason)
            .ToListAsync(ct);

        var claimedFree = claims.Where(c => !c.IsPremium).Select(c => c.TierIndex).Distinct().OrderBy(x => x).ToArray();
        var claimedPremium = claims.Where(c => c.IsPremium).Select(c => c.TierIndex).Distinct().OrderBy(x => x).ToArray();

        return new ProgressionResponse(
            userId,
            progression.Xp,
            CalculateLevel(progression.Xp),
            progression.HasPremiumPass,
            progression.CurrentSeason,
            claimedFree,
            claimedPremium,
            progression.Dust,
            progression.Crystals,
            progression.Inventory);
    }

    private int CalculateLevel(long xp)
    {
        if (_progressionOptions.XpPerLevel <= 0)
        {
            return 0;
        }

        return (int)Math.Floor(xp / (double)_progressionOptions.XpPerLevel);
    }

    private async Task<UserProgression> EnsureProgressionAsync(string userId, CancellationToken ct)
    {
        var progression = await _db.UserProgressions.Find(p => p.Id == userId).FirstOrDefaultAsync(ct);
        var desiredSeason = _battlePassOptions.CurrentSeason;

        if (progression == null)
        {
            progression = new UserProgression
            {
                Id = userId,
                Xp = 0,
                Level = 0,
                HasPremiumPass = false,
                CurrentSeason = desiredSeason,
                Dust = 0,
                Crystals = 0,
                Inventory = new List<string>()
            };
            await _db.UserProgressions.InsertOneAsync(progression, cancellationToken: ct);
            return progression;
        }

        if (progression.CurrentSeason != desiredSeason)
        {
            // Reset seasonal progress but KEEP Dust, Crystals, and Inventory
            progression.Xp = 0;
            progression.Level = 0;
            progression.HasPremiumPass = false;
            progression.CurrentSeason = desiredSeason;
            await _db.UserProgressions.ReplaceOneAsync(p => p.Id == userId, progression, cancellationToken: ct);
        }

        // ensure level stays in sync even if XP changes via other routes
        var expectedLevel = CalculateLevel(progression.Xp);
        if (progression.Level != expectedLevel)
        {
            progression.Level = expectedLevel;
            await _db.UserProgressions.ReplaceOneAsync(p => p.Id == userId, progression, cancellationToken: ct);
        }

        return progression;
    }
}