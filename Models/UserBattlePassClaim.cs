using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameBackend.Api.Models;

public class UserBattlePassClaim
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("season")]
    public int Season { get; set; }

    [BsonElement("tierIndex")]
    public int TierIndex { get; set; }

    [BsonElement("isPremium")]
    public bool IsPremium { get; set; }

    [BsonElement("claimedAt")]
    public DateTime ClaimedAt { get; set; }
}
