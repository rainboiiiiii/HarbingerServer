using GameBackend.Api.Data;
using GameBackend.Api.Models;
using GameBackend.Api.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace GameBackend.Api.Endpoints;

public static class ModerationEndpoints
{
    public static void MapModerationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/moderation");

        group.MapPost("/warn/{targetUserId}", WarnPlayerAsync);
        group.MapPost("/ban/{targetUserId}", BanPlayerAsync);
        group.MapPost("/grant/{targetUserId}", GrantItemsAsync);
        group.MapPost("/promote/{targetUserId}", PromoteUserAsync);
    }

    private static async Task<IResult> WarnPlayerAsync(
        string targetUserId,
        [FromBody] WarnPlayerRequest request,
        MongoDbContext db,
        CancellationToken ct)
    {
        // Check Executor (by Discord ID)
        var executor = await db.Users.Find(u => u.DiscordId == request.ExecutorDiscordId).FirstOrDefaultAsync(ct);
        if (executor == null) return Results.NotFound(new { message = "Executor not found (linked Discord account required)" });

        if (executor.Role != "Mod" && executor.Role != "Admin")
        {
            return Results.Forbid();
        }

        // Check Target (by In-Game Account ID)
        var target = await db.Users.Find(u => u.Id == targetUserId).FirstOrDefaultAsync(ct);
        if (target == null) return Results.NotFound(new { message = "Target user not found" });

        var warning = new Warning
        {
            UserId = target.Id,
            Reason = request.Reason,
            WarnedBy = executor.Id,
            CreatedAt = DateTime.UtcNow
        };

        await db.Warnings.InsertOneAsync(warning, cancellationToken: ct);

        return Results.Ok(new { message = "Player warned", warningId = warning.Id });
    }

    private static async Task<IResult> BanPlayerAsync(
        string targetUserId,
        [FromBody] BanPlayerRequest request,
        MongoDbContext db,
        CancellationToken ct)
    {
        // Check Executor (by Discord ID)
        var executor = await db.Users.Find(u => u.DiscordId == request.ExecutorDiscordId).FirstOrDefaultAsync(ct);
        if (executor == null) return Results.NotFound(new { message = "Executor not found (linked Discord account required)" });

        if (executor.Role != "Mod" && executor.Role != "Admin")
        {
            return Results.Forbid();
        }

        if (executor.Role == "Mod")
        {
            if (request.DurationHours <= 0 || request.DurationHours > 168)
            {
                return Results.BadRequest(new { message = "Mods can only issue temporary bans (1-168 hours)" });
            }
        }

        // Check Target (by In-Game Account ID)
        var target = await db.Users.Find(u => u.Id == targetUserId).FirstOrDefaultAsync(ct);
        if (target == null) return Results.NotFound(new { message = "Target user not found" });

        var updateBuilder = Builders<User>.Update
            .Set(u => u.IsBanned, true)
            .Set(u => u.BanReason, request.Reason);

        if (request.DurationHours > 0)
        {
            updateBuilder = updateBuilder.Set(u => u.BanExpiresAt, DateTime.UtcNow.AddHours(request.DurationHours));
        }
        else
        {
            updateBuilder = updateBuilder.Set(u => u.BanExpiresAt, null); // Permanent
        }
        
        await db.Users.UpdateOneAsync(u => u.Id == target.Id, updateBuilder, cancellationToken: ct);

        return Results.Ok(new { message = $"Player {target.Username} has been banned." });
    }

    private static async Task<IResult> GrantItemsAsync(
        string targetUserId,
        [FromBody] GrantItemsRequest request,
        MongoDbContext db,
        ProgressionService progressionService,
        CancellationToken ct)
    {
        // Check Executor (by Discord ID)
        var executor = await db.Users.Find(u => u.DiscordId == request.ExecutorDiscordId).FirstOrDefaultAsync(ct);
        if (executor == null) return Results.NotFound(new { message = "Executor not found (linked Discord account required)" });

        if (executor.Role != "Admin")
        {
            return Results.Forbid();
        }

        // Check Target (by In-Game Account ID)
        var target = await db.Users.Find(u => u.Id == targetUserId).FirstOrDefaultAsync(ct);
        if (target == null) return Results.NotFound(new { message = "Target user not found" });

        if (request.Dust > 0 || request.Crystals > 0)
        {
            await progressionService.AddRewardsAsync(target.Id, 0, request.Dust, request.Crystals, ct);
        }

        if (request.Items != null && request.Items.Any())
        {
            await progressionService.GrantItemsAsync(target.Id, request.Items, ct);
        }

        if (request.UnlockBattlePass)
        {
             await progressionService.UnlockBattlePassAsync(target.Id, ct);
        }

        return Results.Ok(new { message = "Grant successful" });
    }

    private static async Task<IResult> PromoteUserAsync(
        string targetUserId,
        [FromBody] PromoteUserRequest request,
        MongoDbContext db,
        CancellationToken ct)
    {
        // Check Executor (by Discord ID)
        var executor = await db.Users.Find(u => u.DiscordId == request.ExecutorDiscordId).FirstOrDefaultAsync(ct);
        if (executor == null) return Results.NotFound(new { message = "Executor not found (linked Discord account required)" });

        if (executor.Role != "Admin")
        {
            return Results.Forbid();
        }

        var validRoles = new[] { "Player", "Mod", "Admin" };
        if (!validRoles.Contains(request.NewRole))
        {
            return Results.BadRequest(new { message = "Invalid role. Valid roles: Player, Mod, Admin" });
        }

        // Check Target (by In-Game Account ID)
        var target = await db.Users.Find(u => u.Id == targetUserId).FirstOrDefaultAsync(ct);
        if (target == null) return Results.NotFound(new { message = "Target user not found" });

        var update = Builders<User>.Update.Set(u => u.Role, request.NewRole);
        await db.Users.UpdateOneAsync(u => u.Id == target.Id, update, cancellationToken: ct);

        return Results.Ok(new { message = $"User {target.Username} promoted to {request.NewRole}" });
    }
}
