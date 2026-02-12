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
        var user = await EnsureProgressionAsync(userId, ct);
        return await BuildResponseAsync(userId, user, ct);
    }

    public async Task<ProgressionResponse> AddXpAsync(string userId, long xpToAdd, bool applyToSeasonPass = true, CancellationToken ct = default)
    {
        var user = await EnsureProgressionAsync(userId, ct);
        
        // Account XP is permanent
        user.Xp += xpToAdd;
        user.Level = CalculateLevel(user.Xp);

        // Season Pass XP resets seasonally
        if (applyToSeasonPass)
        {
            user.SeasonPassXp += xpToAdd;
        }

        await _db.Users.ReplaceOneAsync(
            u => u.Id == userId,
            user,
            new ReplaceOptions { IsUpsert = true },
            ct);

        return await BuildResponseAsync(userId, user, ct);
    }

    public async Task<ProgressionResponse> AddRewardsAsync(string userId, long xpToAdd, int dustToAdd, int crystalsToAdd, bool applyToSeasonPass = true, CancellationToken ct = default)
    {
        var user = await EnsureProgressionAsync(userId, ct);
        
        // Account XP
        user.Xp += xpToAdd;
        user.Level = CalculateLevel(user.Xp);

        // Season Pass XP
        if (applyToSeasonPass)
        {
            user.SeasonPassXp += xpToAdd;
        }

        user.Dust += dustToAdd;
        user.Crystals += crystalsToAdd;

        await _db.Users.ReplaceOneAsync(
            u => u.Id == userId,
            user,
            new ReplaceOptions { IsUpsert = true },
            ct);

        return await BuildResponseAsync(userId, user, ct);
    }

    public async Task<ProgressionResponse> GrantItemsAsync(string userId, List<string> items, CancellationToken ct = default)
    {
        var user = await EnsureProgressionAsync(userId, ct);
        bool changed = false;
        foreach (var item in items)
        {
            if (!user.Inventory.Contains(item))
            {
                user.Inventory.Add(item);
                changed = true;
            }
        }

        if (changed)
        {
            await _db.Users.ReplaceOneAsync(u => u.Id == userId, user, new ReplaceOptions { IsUpsert = true }, ct);
        }

        return await BuildResponseAsync(userId, user, ct);
    }

    public async Task<ProgressionResponse> PurchaseItemAsync(string userId, string itemId, string currencyType, int cost, CancellationToken ct = default)
    {
        var user = await EnsureProgressionAsync(userId, ct);

        // If already owned, don't charge, just return current state
        if (user.Inventory.Contains(itemId))
        {
            return await BuildResponseAsync(userId, user, ct);
        }

        if (string.Equals(currencyType, "Dust", StringComparison.OrdinalIgnoreCase))
        {
            if (user.Dust < cost)
            {
                throw new InvalidOperationException("Not enough dust");
            }
            user.Dust -= cost;
        }
        else if (string.Equals(currencyType, "Crystals", StringComparison.OrdinalIgnoreCase))
        {
            if (user.Crystals < cost)
            {
                throw new InvalidOperationException("Not enough crystals");
            }
            user.Crystals -= cost;
        }
        else
        {
            throw new ArgumentException($"Invalid currency type: {currencyType}");
        }

        user.Inventory.Add(itemId);

        await _db.Users.ReplaceOneAsync(
            u => u.Id == userId,
            user,
            new ReplaceOptions { IsUpsert = true },
            ct);

        return await BuildResponseAsync(userId, user, ct);
    }

    public async Task<ProgressionResponse> UnlockBattlePassAsync(string userId, CancellationToken ct = default)
    {
        var user = await EnsureProgressionAsync(userId, ct);
        user.HasPremiumPass = true;
        await _db.Users.ReplaceOneAsync(u => u.Id == userId, user, new ReplaceOptions { IsUpsert = true }, ct);
        return await BuildResponseAsync(userId, user, ct);
    }

    public async Task<(ProgressionResponse response, bool duplicate)> ClaimTierAsync(string userId, int tierIndex, bool isPremium, CancellationToken ct = default)
    {
        var user = await EnsureProgressionAsync(userId, ct);
        if (isPremium && !user.HasPremiumPass)
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
            return (await BuildResponseAsync(userId, user, ct), true);
        }

        return (await BuildResponseAsync(userId, user, ct), false);
    }

    private async Task<ProgressionResponse> BuildResponseAsync(string userId, User user, CancellationToken ct)
    {
        var claims = await _db.UserBattlePassClaims
            .Find(c => c.UserId == userId && c.Season == _battlePassOptions.CurrentSeason)
            .ToListAsync(ct);

        var claimedFree = claims.Where(c => !c.IsPremium).Select(c => c.TierIndex).Distinct().OrderBy(x => x).ToArray();
        var claimedPremium = claims.Where(c => c.IsPremium).Select(c => c.TierIndex).Distinct().OrderBy(x => x).ToArray();

        return new ProgressionResponse(
            userId,
            user.Xp,
            CalculateLevel(user.Xp),
            user.HasPremiumPass,
            user.CurrentSeason,
            claimedFree,
            claimedPremium,
            user.Dust,
            user.Crystals,
            user.Inventory,
            user.SeasonPassXp);
    }

    private int CalculateLevel(long xp)
    {
        if (_progressionOptions.XpPerLevel <= 0)
        {
            return 0;
        }

        return (int)Math.Floor(xp / (double)_progressionOptions.XpPerLevel);
    }

    private async Task<User> EnsureProgressionAsync(string userId, CancellationToken ct)
    {
        var user = await _db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(ct);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

        var desiredSeason = _battlePassOptions.CurrentSeason;
        bool needsUpdate = false;

        if (user.CurrentSeason != desiredSeason)
        {
            // Reset SEASONAL progress (Season Pass)
            // But KEEP Account XP and Account Level (Permanent)
            user.SeasonPassXp = 0;
            user.HasPremiumPass = false;
            user.CurrentSeason = desiredSeason;
            needsUpdate = true;
        }

        // ensure level stays in sync even if XP changes via other routes
        var expectedLevel = CalculateLevel(user.Xp);
        if (user.Level != expectedLevel)
        {
            user.Level = expectedLevel;
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            await _db.Users.ReplaceOneAsync(u => u.Id == userId, user, cancellationToken: ct);
        }

        return user;
    }
}