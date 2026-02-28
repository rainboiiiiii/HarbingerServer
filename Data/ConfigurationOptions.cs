namespace GameBackend.Api.Data;

public class MongoOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
}

public class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int ExpireMinutes { get; set; } = 60;
}

public class ProgressionOptions
{
    public int AccountXpPerLevel { get; set; } = 100000;
    public int SeasonPassXpPerLevel { get; set; } = 50000;
    public RewardScalingOptions RewardScaling { get; set; } = new();
}

public class RewardScalingOptions
{
    public long XpPerWave { get; set; } = 100;
    public long XpPerKill { get; set; } = 2;
    public int DustPerWave { get; set; } = 50;
    public int CrystalsPerWave { get; set; } = 5;
    public long MaxXpPerMatch { get; set; } = 50000;
    public int MaxDustPerMatch { get; set; } = 5000;
    public int MaxCrystalsPerMatch { get; set; } = 500;
}

public class BattlePassOptions
{
    public int TotalTiers { get; set; } = 50;
    public int CurrentSeason { get; set; } = 1;
    public List<BattlePassTierDefinition> Tiers { get; set; } = new();
}

public class BattlePassTierDefinition
{
    public int TierIndex { get; set; }
    public RewardDefinition? FreeReward { get; set; }
    public RewardDefinition? PremiumReward { get; set; }
}

public class RewardDefinition
{
    public string Type { get; set; } = "Dust"; // Dust, Crystals, Item
    public string? ItemId { get; set; }
    public int Amount { get; set; } = 1;
}

public class EconomyOptions
{
    public List<ItemDefinition> ItemRegistry { get; set; } = new();
    public List<WeaponDefinition> WeaponRegistry { get; set; } = new();
    public List<ItemPrice> ItemPrices { get; set; } = new();
}

public class ItemDefinition
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public int MaxStack { get; set; } = 999;
}

public class WeaponDefinition
{
    public string WeaponId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Primary"; // Primary, Secondary, Melee
}

public class ItemPrice
{
    public string ItemId { get; set; } = string.Empty;
    public string Currency { get; set; } = "Dust";
    public int Cost { get; set; }
}

public class MatchmakingOptions
{
    public int DefaultPlayersPerMatch { get; set; } = 4;
    public List<string> AvailableMaps { get; set; } = new() { "Outer Edge" };
}

public class CorsOptions
{
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}

public class AdminOptions
{
    public string ApiKey { get; set; } = string.Empty;
}

public class GameServerOptions
{
    public string ExecutablePath { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string UnityExecutableName { get; set; } = "UnityHeadlessServer";
}

public class UnityMatchmakingOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ServiceAccountKey { get; set; } = string.Empty;
}
