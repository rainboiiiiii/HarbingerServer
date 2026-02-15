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
        group.MapGet("/weapons", GetWeaponRegistry).AllowAnonymous().WithOpenApi();
        group.MapGet("/weapons/verify/{weaponId}", VerifyWeaponAsync).WithOpenApi();
    }

    private static IResult GetWeaponRegistry(IOptions<EconomyOptions> economyOptions)
    {
        return ApiResults.Ok(economyOptions.Value.WeaponRegistry);
    }

    private static async Task<IResult> VerifyWeaponAsync(
        string weaponId,
        HttpContext context,
        ProgressionService progressionService,
        CancellationToken ct)
    {
        var userId = GetUserId(context);
        var owned = await progressionService.VerifyWeaponOwnershipAsync(userId, weaponId, ct);
        return ApiResults.Ok(new { weaponId, owned });
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
        if (request is null)
        {
            return ApiResults.Error("Invalid request", StatusCodes.Status400BadRequest);
        }

        var userId = GetUserId(context);
        var progression = await progressionService.AddXpAsync(userId, request.AccountXp, request.SeasonPassXp, ct);
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
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("required"))
            {
                return ApiResults.Error(ex.Message, StatusCodes.Status403Forbidden);
            }
            return ApiResults.Error("Premium pass required", StatusCodes.Status403Forbidden);
        }
    }

    private static string GetUserId(HttpContext context)
    {
        return context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
               context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ??
               context.User.Identity?.Name ??
               string.Empty;
    }
}
