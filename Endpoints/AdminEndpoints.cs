using GameBackend.Api.Data;
using GameBackend.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GameBackend.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin");

        group.MapPost("/set-role", SetUserRoleAsync);
    }

    private static async Task<IResult> SetUserRoleAsync(
        [FromHeader(Name = "X-Admin-Key")] string apiKey,
        [FromBody] SetRoleRequest request,
        MongoDbContext db,
        IOptions<AdminOptions> adminOptions,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey != adminOptions.Value.ApiKey)
        {
            return Results.Unauthorized();
        }

        var validRoles = new[] { "Player", "Mod", "Admin" };
        if (!validRoles.Contains(request.Role))
        {
            return Results.BadRequest(new { message = "Invalid role. Valid roles: Player, Mod, Admin" });
        }

        var target = await db.Users.Find(u => u.Id == request.GameId.ToString()).FirstOrDefaultAsync(ct);
        if (target == null)
        {
            return Results.NotFound(new { message = "User not found" });
        }

        var updateBuilder = Builders<User>.Update.Set(u => u.Role, request.Role);

        // If DiscordId is provided in the request, link it to the user
        if (!string.IsNullOrWhiteSpace(request.DiscordId))
        {
            // Optional: Check if this Discord ID is already linked to another user to prevent duplicates
            var existingDiscordUser = await db.Users.Find(u => u.DiscordId == request.DiscordId && u.Id != target.Id).FirstOrDefaultAsync(ct);
            if (existingDiscordUser != null)
            {
                 return Results.Conflict(new { message = $"Discord ID {request.DiscordId} is already linked to user {existingDiscordUser.Username} (ID: {existingDiscordUser.Id})" });
            }

            updateBuilder = updateBuilder.Set(u => u.DiscordId, request.DiscordId);
        }

        await db.Users.UpdateOneAsync(u => u.Id == target.Id, updateBuilder, cancellationToken: ct);

        return Results.Ok(new { message = $"User {target.Username} (ID: {target.Id}) role set to {request.Role}" + (!string.IsNullOrWhiteSpace(request.DiscordId) ? " and Discord ID linked." : ".") });
    }
}

public class SetRoleRequest
{
    public long GameId { get; set; }
    public string Role { get; set; } = "Player";
    public string? DiscordId { get; set; } // Optional: Link Discord ID while setting role
}
