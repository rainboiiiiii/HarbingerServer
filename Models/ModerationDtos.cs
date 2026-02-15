namespace GameBackend.Api.Models;

public class WarnPlayerRequest
{
    public string ExecutorDiscordId { get; set; } = string.Empty; // Who is issuing the warning (Discord ID)
    public string Reason { get; set; } = string.Empty;
}

public class BanPlayerRequest
{
    public string ExecutorDiscordId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int DurationHours { get; set; } // 0 = permanent
}

public class GrantItemsRequest
{
    public string ExecutorDiscordId { get; set; } = string.Empty;
    public int Dust { get; set; }
    public int Crystals { get; set; }
    public List<string> Items { get; set; } = new();
    public List<string> Weapons { get; set; } = new();
    public bool UnlockBattlePass { get; set; }
    public int Quantity { get; set; } = 1;
}

public class PromoteUserRequest
{
    public string ExecutorDiscordId { get; set; } = string.Empty;
    public string NewRole { get; set; } = "Player"; // Player, Mod, Admin
}
