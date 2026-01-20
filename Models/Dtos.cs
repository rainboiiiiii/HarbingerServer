namespace GameBackend.Api.Models;

public record SignupRequest(string Username, string Password);

public record LoginRequest(string Username, string Password);

public record AuthResponse(string UserId, string Username, string Token, DateTime ExpiresAt);

public record MeResponse(string Id, string Username, DateTime ExpiresAt, int Dust, int Crystals, List<string> Inventory, ProgressionResponse Progression);

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
    List<string> Inventory);

public class AddXpRequest
{
    public long Xp { get; set; }
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
    public IReadOnlyCollection<string> Players { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
    public string State { get; set; } = string.Empty;
}

public class MatchReportRequest
{
    public string MatchId { get; set; } = string.Empty;
    public string? LobbyId { get; set; }
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
}

public class MatchReportResponse
{
    public string MatchId { get; set; } = string.Empty;
    public List<MatchAwardResponse> Awards { get; set; } = new();
}

public class MatchAwardResponse
{
    public string UserId { get; set; } = string.Empty;
    public long XpAwarded { get; set; }
    public int DustAwarded { get; set; }
    public int CrystalsAwarded { get; set; }
    public long NewXp { get; set; }
    public int NewLevel { get; set; }
    public int NewDust { get; set; }
    public int NewCrystals { get; set; }
}