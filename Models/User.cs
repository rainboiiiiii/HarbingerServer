using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameBackend.Api.Models;

[BsonIgnoreExtraElements]
public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("unityPlayerId")]
    public string? UnityPlayerId { get; set; } // The GUID from UGS

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

    [BsonElement("xp")]
    public long Xp { get; set; }

    [BsonElement("level")]
    public int Level { get; set; }

    [BsonElement("dust")]
    public int Dust { get; set; }

    [BsonElement("crystals")]
    public int Crystals { get; set; }

    [BsonElement("inventory")]
    public List<InventoryItem> Inventory { get; set; } = new();

    [BsonElement("ownedWeapons")]
    public List<string> OwnedWeapons { get; set; } = new();

    [BsonElement("hasPremiumPass")]
    public bool HasPremiumPass { get; set; }

    [BsonElement("currentSeason")]
    public int CurrentSeason { get; set; }

    [BsonElement("seasonPassXp")]
    public long SeasonPassXp { get; set; }

    [BsonElement("seasonPassLevel")]
    public int SeasonPassLevel { get; set; }

    [BsonElement("lastClaimedAt")]
    public DateTime? LastClaimedAt { get; set; }
}

public class InventoryItem
{
    [BsonElement("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [BsonElement("quantity")]
    public int Quantity { get; set; } = 1;

    [BsonElement("acquiredAt")]
    public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;
}
