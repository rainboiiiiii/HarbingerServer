using System.IdentityModel.Tokens.Jwt;
using GameBackend.Api.Data;
using GameBackend.Api.Models;
using GameBackend.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;

namespace GameBackend.Api.Endpoints;

public static class ProgressionEndpoints
{
    public static void MapProgressionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/progression").RequireAuthorization();

        group.MapGet("/me", GetProgressionAsync).WithOpenApi();
        group.MapPost("/addxp", AddXpAsync).WithOpenApi();
        group.MapPost("/unlock-battlepass", UnlockAsync).WithOpenApi();
        group.MapPost("/claim", ClaimAsync).WithOpenApi();
    }

    private static async Task<IResult> GetProgressionAsync(
        HttpContext context,
        ProgressionService progressionService,
        CancellationToken ct)
    {
        var userId = GetUserId(context);
        var progression = await progressionService.GetProgressionAsync(userId, ct);
        return ApiResults.Ok(progression);
    }

    private static async Task<IResult> AddXpAsync(
        HttpContext context,
        AddXpRequest request,
        ProgressionService progressionService,
        CancellationToken ct)
    {
        if (request is null || request.Xp <= 0 || request.Xp > 1_000_000)
        {
            return ApiResults.Error("xp must be between 1 and 1000000", StatusCodes.Status400BadRequest);
        }

        var userId = GetUserId(context);
        var progression = await progressionService.AddXpAsync(userId, request.Xp, ct);
        return ApiResults.Ok(progression);
    }

    private static async Task<IResult> UnlockAsync(
        HttpContext context,
        ProgressionService progressionService,
        CancellationToken ct)
    {
        var userId = GetUserId(context);
        var progression = await progressionService.UnlockBattlePassAsync(userId, ct);
        return ApiResults.Ok(progression);
    }

    private static async Task<IResult> ClaimAsync(
        HttpContext context,
        ClaimTierRequest request,
        ProgressionService progressionService,
        IOptions<BattlePassOptions> battlePassOptions,
        CancellationToken ct)
    {
        if (request is null)
        {
            return ApiResults.Error("Request body required", StatusCodes.Status400BadRequest);
        }

        if (request.TierIndex < 0 || request.TierIndex >= battlePassOptions.Value.TotalTiers)
        {
            return ApiResults.Error("Tier out of range", StatusCodes.Status400BadRequest);
        }

        var userId = GetUserId(context);
        try
        {
            var (response, duplicate) = await progressionService.ClaimTierAsync(userId, request.TierIndex, request.IsPremium, ct);
            if (duplicate)
            {
                return ApiResults.Error("Tier already claimed", StatusCodes.Status409Conflict);
            }

            return ApiResults.Ok(response);
        }
        catch (InvalidOperationException)
        {
            return ApiResults.Error("Premium pass required", StatusCodes.Status403Forbidden);
        }
    }

    private static string GetUserId(HttpContext context)
    {
        return context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ??
               context.User.Identity?.Name ??
               string.Empty;
    }
}
