namespace GameBackend.Api.Models;

public record SignupRequest(string Username, string Password);

public record LoginRequest(string Username, string Password);

public record AuthResponse(string UserId, string Username, string Token, DateTime ExpiresAt);

public record MeResponse(string Id, string Username, DateTime ExpiresAt, int Dust, int Crystals, List<InventoryItem> Inventory, List<string> OwnedWeapons, ProgressionResponse Progression);

public record ProgressionResponse(
    string UserId,
    long Xp,
    int Level,
    bool HasPremiumPass,
    int CurrentSeason,
    IReadOnlyCollection<int> ClaimedFreeTiers,
    IReadOnlyCollection<int> ClaimedPremiumTiers,
    int Dust,
    int Crystals,
    List<InventoryItem> Inventory,
    List<string> OwnedWeapons,
    long SeasonPassXp = 0,
    int SeasonPassLevel = 0);

public class AddXpRequest
{
    public long AccountXp { get; set; }
    public long SeasonPassXp { get; set; }
}

public class ClaimTierRequest
{
    public int TierIndex { get; set; }
    public bool IsPremium { get; set; }
}

public class EnqueueRequest
{
    public string Mode { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public int PlayersPerMatch { get; set; } = 4;
    public List<string> EquippedWeapons { get; set; } = new();
    public List<string> EquippedAbilities { get; set; } = new();
}

public class MatchStatusResponse
{
    public string Status { get; set; } = "idle";
    public QueueStatus? Queue { get; set; }
    public MatchInfo? Match { get; set; }
}

public class QueueStatus
{
    public string TicketId { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public DateTime EnqueuedAt { get; set; }
}

public class MatchInfo
{
    public string MatchId { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Map { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Players { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
    public string State { get; set; } = string.Empty;
    public string? ServerIp { get; set; }
    public int? ServerPort { get; set; }
}

public class MatchReportRequest
{
    public string MatchId { get; set; } = string.Empty;
    public string? LobbyId { get; set; }
    public string Map { get; set; } = string.Empty;
    public string HostId { get; set; } = string.Empty;
    public List<PlayerSummary> PlayerSummaries { get; set; } = new();
}

public class PlayerSummary
{
    public string UserId { get; set; } = string.Empty;
    public int WavesCleared { get; set; }
    public int Kills { get; set; }
    public int DurationSeconds { get; set; }
}

public class PurchaseRequest
{
    public string ItemId { get; set; } = string.Empty;
    public string Currency { get; set; } = "Dust";
    public int Cost { get; set; }
    public int Quantity { get; set; } = 1;
}

public class MatchReportResponse
{
    public string MatchId { get; set; } = string.Empty;
    public List<MatchAwardResponse> Awards { get; set; } = new();
}

public class MatchAwardResponse
{
    public string UserId { get; set; } = string.Empty;
    public long AccountXpAwarded { get; set; }
    public long SeasonPassXpAwarded { get; set; }
    public int DustAwarded { get; set; }
    public int CrystalsAwarded { get; set; }
    public long NewXp { get; set; }
    public int NewLevel { get; set; }
    public int NewDust { get; set; }
    public int NewCrystals { get; set; }
    public long NewSeasonPassXp { get; set; }
    public int NewSeasonPassLevel { get; set; }
}