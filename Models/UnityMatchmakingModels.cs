namespace GameBackend.Api.Models;

public class UnityMatchmakingTicketRequest
{
    public string QueueName { get; set; } = string.Empty;
    public Dictionary<string, object> Attributes { get; set; } = new();
    public List<UnityMatchmakingPlayer> Players { get; set; } = new();
}

public class UnityMatchmakingPlayer
{
    public string Id { get; set; } = string.Empty;
    public Dictionary<string, object> Attributes { get; set; } = new();
}

public class UnityMatchmakingTicketResponse
{
    public string Id { get; set; } = string.Empty;
}

public class UnityMatchmakingTicketStatusResponse
{
    public string AssignmentType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // e.g., "Found", "NotFound", "Pending"
    public string Ip { get; set; } = string.Empty;
    public int Port { get; set; }
    public string MatchId { get; set; } = string.Empty;
}

public class UnityMatchmakingResultsResponse
{
    public Dictionary<string, object> MatchProperties { get; set; } = new();
    public string? GeneratorName { get; set; }
    public string? QueueName { get; set; }
    public string? PoolName { get; set; }
    public string? EnvironmentId { get; set; }
    public string? BackfillTicketId { get; set; }
    public string? MatchId { get; set; }
    public string? PoolId { get; set; }
}
