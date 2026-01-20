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

    [BsonElement("state")]
    public string State { get; set; } = "active";

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("players")]
    public List<string> Players { get; set; } = new();
}
