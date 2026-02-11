using GameBackend.Api.Data;
using GameBackend.Api.Models;
using GameBackend.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace GameBackend.Api.Endpoints;

public static class DiscordEndpoints
{
    public static void MapDiscordEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/players/discord");

        group.MapGet("/{discordId}", GetProfileAsync);
        group.MapPost("/{discordId}/currency", GiveCurrencyAsync);
        group.MapPost("/{discordId}/ban", BanPlayerAsync);
    }

    private static async Task<IResult> GetProfileAsync(
        string discordId,
        MongoDbContext db,
        ProgressionService progressionService,
        CancellationToken ct)
    {
        var user = await db.Users.Find(u => u.DiscordId == discordId).FirstOrDefaultAsync(ct);
        if (user == null)
        {
            return Results.NotFound(new { message = "User not found with this Discord ID" });
        }

        var progression = await progressionService.GetProgressionAsync(user.Id, ct);

        var response = new DiscordProfileResponse
        {
            UserId = user.Id,
            Username = user.Username,
            DiscordId = user.DiscordId,
            IsBanned = user.IsBanned,
            CreatedAt = user.CreatedAt,
            LastLogin = user.LastLogin,
            Level = progression.Level,
            Xp = progression.Xp,
            Dust = progression.Dust,
            Crystals = progression.Crystals
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> GiveCurrencyAsync(
        string discordId,
        [FromBody] DiscordCurrencyRequest request,
        MongoDbContext db,
        ProgressionService progressionService,
        CancellationToken ct)
    {
        var user = await db.Users.Find(u => u.DiscordId == discordId).FirstOrDefaultAsync(ct);
        if (user == null)
        {
            return Results.NotFound(new { message = "User not found with this Discord ID" });
        }

        await progressionService.AddRewardsAsync(user.Id, 0, request.Dust, request.Crystals, ct);

        // Get updated progression
        var progression = await progressionService.GetProgressionAsync(user.Id, ct);

        return Results.Ok(new 
        { 
            message = "Currency added successfully", 
            newBalance = new { dust = progression.Dust, crystals = progression.Crystals } 
        });
    }

    private static async Task<IResult> BanPlayerAsync(
        string discordId,
        [FromBody] DiscordBanRequest request,
        MongoDbContext db,
        CancellationToken ct)
    {
        var user = await db.Users.Find(u => u.DiscordId == discordId).FirstOrDefaultAsync(ct);
        if (user == null)
        {
            return Results.NotFound(new { message = "User not found with this Discord ID" });
        }

        if (user.IsBanned && (!user.BanExpiresAt.HasValue || user.BanExpiresAt > DateTime.UtcNow))
        {
             return Results.Ok(new { message = "Player is already banned" });
        }

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
        
        await db.Users.UpdateOneAsync(u => u.Id == user.Id, updateBuilder, cancellationToken: ct);

        return Results.Ok(new { message = $"Player {user.Username} has been banned.", reason = request.Reason });
    }
}
