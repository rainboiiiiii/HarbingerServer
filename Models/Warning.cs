using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameBackend.Api.Models;

public class Warning
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("reason")]
    public string Reason { get; set; } = string.Empty;

    [BsonElement("warnedBy")]
    public string WarnedBy { get; set; } = string.Empty; // Moderator User ID

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("acknowledged")]
    public bool Acknowledged { get; set; } // If the player has seen it
}
