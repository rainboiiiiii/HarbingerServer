using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameBackend.Api.Models;

public class UserProgression
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty; // userId

    [BsonElement("xp")]
    public long Xp { get; set; }

    [BsonElement("level")]
    public int Level { get; set; }

    [BsonElement("dust")]
    public int Dust { get; set; }

    [BsonElement("crystals")]
    public int Crystals { get; set; }

    [BsonElement("inventory")]
    public List<string> Inventory { get; set; } = new();

    [BsonElement("hasPremiumPass")]
    public bool HasPremiumPass { get; set; }

    [BsonElement("currentSeason")]
    public int CurrentSeason { get; set; }
}
