using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameBackend.Api.Models;

public class Counter
{
    [BsonId]
    public string Id { get; set; } = string.Empty; // e.g., "users"

    [BsonElement("seq")]
    public long Seq { get; set; }
}
