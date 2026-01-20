using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameBackend.Api.Models;

public class QueueTicket
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("mode")]
    public string Mode { get; set; } = string.Empty;

    [BsonElement("region")]
    public string Region { get; set; } = string.Empty;

    [BsonElement("playersPerMatch")]
    public int PlayersPerMatch { get; set; }

    [BsonElement("enqueuedAt")]
    public DateTime EnqueuedAt { get; set; }

    [BsonElement("state")]
    public string State { get; set; } = "queued";

    [BsonElement("matchId")]
    public string? MatchId { get; set; }
}
