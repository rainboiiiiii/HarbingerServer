using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameBackend.Api.Models;

public class Match
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [BsonElement("mode")]
    public string Mode { get; set; } = string.Empty;

    [BsonElement("region")]
    public string Region { get; set; } = string.Empty;

    [BsonElement("map")]
    public string Map { get; set; } = string.Empty;

    [BsonElement("state")]
    public string State { get; set; } = "active";

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("players")]
    public List<string> Players { get; set; } = new();

    [BsonElement("serverIp")]
    public string? ServerIp { get; set; }

    [BsonElement("serverPort")]
    public int? ServerPort { get; set; }

    // Unity Matchmaking related fields
    [BsonElement("generatorName")]
    public string? GeneratorName { get; set; }

    [BsonElement("queueName")]
    public string? QueueName { get; set; }

    [BsonElement("poolName")]
    public string? PoolName { get; set; }

    [BsonElement("environmentId")]
    public string? EnvironmentId { get; set; }

    [BsonElement("backfillTicketId")]
    public string? BackfillTicketId { get; set; }

    [BsonElement("poolId")]
    public string? PoolId { get; set; }

    [BsonElement("matchProperties")]
    public Dictionary<string, object>? MatchProperties { get; set; }
}
