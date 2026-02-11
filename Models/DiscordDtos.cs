namespace GameBackend.Api.Models;

public class DiscordCurrencyRequest
{
    public int Dust { get; set; }
    public int Crystals { get; set; }
}

public class DiscordBanRequest
{
    public string Reason { get; set; } = string.Empty;
    public int DurationHours { get; set; } // 0 = permanent
}

public class DiscordProfileResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? DiscordId { get; set; }
    public bool IsBanned { get; set; }
    public int Level { get; set; }
    public long Xp { get; set; }
    public int Dust { get; set; }
    public int Crystals { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
}
