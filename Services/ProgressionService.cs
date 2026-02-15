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
    private readonly EconomyOptions _economyOptions;
    private readonly ILogger<ProgressionService> _logger;

    public ProgressionService(
        MongoDbContext db,
        IOptions<ProgressionOptions> progressionOptions,
        IOptions<BattlePassOptions> battlePassOptions,
        IOptions<EconomyOptions> economyOptions,
        ILogger<ProgressionService> logger)
    {
        _db = db;
        _progressionOptions = progressionOptions.Value;
        _battlePassOptions = battlePassOptions.Value;
        _economyOptions = economyOptions.Value;
        _logger = logger;
    }

    public async Task<ProgressionResponse> GetProgressionAsync(string userId, CancellationToken ct = default)
    {
        var user = await EnsureProgressionAsync(userId, ct);
        return await BuildResponseAsync(userId, user, ct);
    }

    public async Task<ProgressionResponse> AddXpAsync(string userId, long accountXpToAdd, long seasonPassXpToAdd = 0, CancellationToken ct = default)
    {
        var user = await EnsureProgressionAsync(userId, ct);
        
        // Account XP is permanent
        user.Xp += accountXpToAdd;
        user.Level = CalculateAccountLevel(user.Xp);

        // Season Pass XP
        if (seasonPassXpToAdd > 0)
        {
            user.SeasonPassXp += seasonPassXpToAdd;
            user.SeasonPassLevel = CalculateSeasonPassLevel(user.SeasonPassXp);
        }

        await _db.Users.ReplaceOneAsync(
            u => u.Id == userId,
            user,
            new ReplaceOptions { IsUpsert = true },
            ct);

        return await BuildResponseAsync(userId, user, ct);
    }

    public async Task<ProgressionResponse> AddRewardsAsync(string userId, long accountXpToAdd, long seasonPassXpToAdd, int dustToAdd, int crystalsToAdd, CancellationToken ct = default)
    {
        var user = await EnsureProgressionAsync(userId, ct);
        
        // Account XP
        user.Xp += accountXpToAdd;
        user.Level = CalculateAccountLevel(user.Xp);

        // Season Pass XP
        user.SeasonPassXp += seasonPassXpToAdd;
        user.SeasonPassLevel = CalculateSeasonPassLevel(user.SeasonPassXp);

        user.Dust += dustToAdd;
        user.Crystals += crystalsToAdd;

        await _db.Users.ReplaceOneAsync(
            u => u.Id == userId,
            user,
            new ReplaceOptions { IsUpsert = true },
            ct);

        return await BuildResponseAsync(userId, user, ct);
    }

    public async Task<ProgressionResponse> GrantItemsAsync(string userId, List<string> itemIds, int quantity = 1, CancellationToken ct = default)
    {
        var user = await EnsureProgressionAsync(userId, ct);
        
        foreach (var itemId in itemIds)
        {
            AddItemToInventory(user, itemId, quantity);
        }

        await _db.Users.ReplaceOneAsync(u => u.Id == userId, user, new ReplaceOptions { IsUpsert = true }, ct);
        return await BuildResponseAsync(userId, user, ct);
    }

    public async Task<ProgressionResponse> GrantWeaponsAsync(string userId, List<string> weaponIds, CancellationToken ct = default)
    {
        var user = await EnsureProgressionAsync(userId, ct);
        
        foreach (var weaponId in weaponIds)
        {
            AddWeaponToOwned(user, weaponId);
        }

        await _db.Users.ReplaceOneAsync(u => u.Id == userId, user, new ReplaceOptions { IsUpsert = true }, ct);
        return await BuildResponseAsync(userId, user, ct);
    }

    public async Task<bool> VerifyWeaponOwnershipAsync(string userId, string weaponId, CancellationToken ct = default)
    {
        var user = await EnsureProgressionAsync(userId, ct);
        return user.OwnedWeapons.Contains(weaponId);
    }

    public async Task<ProgressionResponse> PurchaseItemAsync(string userId, string itemId, string currencyType, int clientCost, int quantity = 1, CancellationToken ct = default)
    {
        var user = await EnsureProgressionAsync(userId, ct);

        // Server-side cost verification
        var itemPrice = _economyOptions.ItemPrices.FirstOrDefault(p => 
            string.Equals(p.ItemId, itemId, StringComparison.OrdinalIgnoreCase) && 
            string.Equals(p.Currency, currencyType, StringComparison.OrdinalIgnoreCase));

        if (itemPrice == null)
        {
            throw new InvalidOperationException($"Item '{itemId}' is not available for purchase with '{currencyType}'");
        }

        int totalCost = itemPrice.Cost * quantity;

        if (string.Equals(currencyType, "Dust", StringComparison.OrdinalIgnoreCase))
        {
            if (user.Dust < totalCost)
            {
                throw new InvalidOperationException("Not enough dust");
            }
            user.Dust -= totalCost;
        }
        else if (string.Equals(currencyType, "Crystals", StringComparison.OrdinalIgnoreCase))
        {
            if (user.Crystals < totalCost)
            {
                throw new InvalidOperationException("Not enough crystals");
            }
            user.Crystals -= totalCost;
        }
        else
        {
            throw new ArgumentException($"Invalid currency type: {currencyType}");
        }

        AddItemToInventory(user, itemId, quantity);

        await _db.Users.ReplaceOneAsync(
            u => u.Id == userId,
            user,
            new ReplaceOptions { IsUpsert = true },
            ct);

        return await BuildResponseAsync(userId, user, ct);
    }

    private void AddItemToInventory(User user, string itemId, int quantity)
    {
        // REQUIREMENT: Check against ItemRegistry to prevent spawning invalid items
        var itemDef = _economyOptions.ItemRegistry.FirstOrDefault(i => 
            string.Equals(i.ItemId, itemId, StringComparison.OrdinalIgnoreCase));

        if (itemDef == null)
        {
            _logger.LogWarning("Attempted to add invalid item ID: {ItemId} to user {UserId}", itemId, user.Id);
            throw new InvalidOperationException($"Item '{itemId}' does not exist in the game database.");
        }

        int maxStack = itemDef.MaxStack;

        // Find existing stacks of this item that aren't full
        var existingStack = user.Inventory.FirstOrDefault(i => i.ItemId == itemId && i.Quantity < maxStack);

        if (existingStack != null)
        {
            int spaceInStack = maxStack - existingStack.Quantity;
            int amountToAdd = Math.Min(quantity, spaceInStack);
            
            existingStack.Quantity += amountToAdd;
            quantity -= amountToAdd;
        }

        // If there's still quantity left to add (or no existing stack found)
        while (quantity > 0)
        {
            int amountToAdd = Math.Min(quantity, maxStack);
            user.Inventory.Add(new InventoryItem
            {
                ItemId = itemId,
                Quantity = amountToAdd,
                AcquiredAt = DateTime.UtcNow
            });
            quantity -= amountToAdd;
        }
    }

    private void AddWeaponToOwned(User user, string weaponId)
    {
        // REQUIREMENT: Check against WeaponRegistry to prevent spawning invalid weapons
        var weaponDef = _economyOptions.WeaponRegistry.FirstOrDefault(w => 
            string.Equals(w.WeaponId, weaponId, StringComparison.OrdinalIgnoreCase));

        if (weaponDef == null)
        {
            _logger.LogWarning("Attempted to grant invalid weapon ID: {WeaponId} to user {UserId}", weaponId, user.Id);
            throw new InvalidOperationException($"Weapon '{weaponId}' does not exist in the game database.");
        }

        if (!user.OwnedWeapons.Contains(weaponId))
        {
            user.OwnedWeapons.Add(weaponId);
        }
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

        // Requirement 1: Premium Pass check
        if (isPremium && !user.HasPremiumPass)
        {
            throw new InvalidOperationException("Premium pass not unlocked");
        }

        // Requirement 2: Season Pass Level check (Tier index starts at 0, so Tier 1 is index 0)
        // Level 1 allows claiming Tier 1 (index 0), Level 2 allows Tier 2 (index 1), etc.
        if (user.SeasonPassLevel < (tierIndex + 1))
        {
            throw new InvalidOperationException($"Season Pass Level {tierIndex + 1} required to claim this tier");
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

            // Grant Reward
            var tierDef = _battlePassOptions.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex);
            if (tierDef != null)
            {
                var reward = isPremium ? tierDef.PremiumReward : tierDef.FreeReward;
                if (reward != null)
                {
                    switch (reward.Type.ToLower())
                    {
                        case "dust":
                            user.Dust += reward.Amount;
                            break;
                        case "crystals":
                            user.Crystals += reward.Amount;
                            break;
                        case "item":
                            if (!string.IsNullOrEmpty(reward.ItemId))
                            {
                                AddItemToInventory(user, reward.ItemId, reward.Amount);
                            }
                            break;
                    }

                    // Save the user state with the new rewards
                    await _db.Users.ReplaceOneAsync(u => u.Id == userId, user, new ReplaceOptions { IsUpsert = true }, ct);
                }
            }
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
            CalculateAccountLevel(user.Xp),
            user.HasPremiumPass,
            user.CurrentSeason,
            claimedFree,
            claimedPremium,
            user.Dust,
            user.Crystals,
            user.Inventory,
            user.OwnedWeapons,
            user.SeasonPassXp,
            CalculateSeasonPassLevel(user.SeasonPassXp));
    }

    private int CalculateAccountLevel(long xp)
    {
        if (_progressionOptions.AccountXpPerLevel <= 0) return 0;
        return (int)Math.Floor(xp / (double)_progressionOptions.AccountXpPerLevel);
    }

    private int CalculateSeasonPassLevel(long xp)
    {
        if (_progressionOptions.SeasonPassXpPerLevel <= 0) return 0;
        return (int)Math.Floor(xp / (double)_progressionOptions.SeasonPassXpPerLevel);
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
            user.SeasonPassLevel = 0;
            user.HasPremiumPass = false;
            user.CurrentSeason = desiredSeason;
            needsUpdate = true;
        }

        // ensure levels stay in sync even if XP changes via other routes
        var expectedLevel = CalculateAccountLevel(user.Xp);
        if (user.Level != expectedLevel)
        {
            user.Level = expectedLevel;
            needsUpdate = true;
        }

        var expectedPassLevel = CalculateSeasonPassLevel(user.SeasonPassXp);
        if (user.SeasonPassLevel != expectedPassLevel)
        {
            user.SeasonPassLevel = expectedPassLevel;
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            await _db.Users.ReplaceOneAsync(u => u.Id == userId, user, cancellationToken: ct);
        }

        return user;
    }
}