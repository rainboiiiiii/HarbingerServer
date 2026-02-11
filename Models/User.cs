using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameBackend.Api.Models;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("usernameNormalized")]
    public string UsernameNormalized { get; set; } = string.Empty;

    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [BsonElement("lastLogin")]
    public DateTime? LastLogin { get; set; }

    [BsonElement("discordId")]
    public string? DiscordId { get; set; }

    [BsonElement("role")]
    public string Role { get; set; } = "Player"; // Player, Mod, Admin

    [BsonElement("isBanned")]
    public bool IsBanned { get; set; }

    [BsonElement("banReason")]
    public string? BanReason { get; set; }

    [BsonElement("banExpiresAt")]
    public DateTime? BanExpiresAt { get; set; }
}
