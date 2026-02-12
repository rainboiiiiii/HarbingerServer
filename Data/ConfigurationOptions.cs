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
    public int XpPerLevel { get; set; } = 1000;
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
}

public class MatchmakingOptions
{
    public int DefaultPlayersPerMatch { get; set; } = 4;
}

public class CorsOptions
{
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}

public class AdminOptions
{
    public string ApiKey { get; set; } = string.Empty;
}
