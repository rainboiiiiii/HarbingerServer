using System.IdentityModel.Tokens.Jwt;
using GameBackend.Api.Models;
using GameBackend.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace GameBackend.Api.Endpoints;

public static class MatchmakingEndpoints
{
    public static void MapMatchmakingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/matchmaking").RequireAuthorization();

        group.MapPost("/enqueue", EnqueueAsync).WithOpenApi();
        group.MapPost("/cancel", CancelAsync).WithOpenApi();
        group.MapGet("/status", StatusAsync).WithOpenApi();
        group.MapGet("/match/{matchId}", GetMatchAsync).WithOpenApi();
    }

    private static async Task<IResult> EnqueueAsync(
        HttpContext context,
        EnqueueRequest request,
        MatchmakingService matchmakingService,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Mode) || string.IsNullOrWhiteSpace(request.Region))
        {
            return ApiResults.Error("mode and region are required", StatusCodes.Status400BadRequest);
        }

        var playersPerMatch = request.PlayersPerMatch <= 0 ? 4 : request.PlayersPerMatch;
        if (playersPerMatch < 2 || playersPerMatch > 16)
        {
            return ApiResults.Error("playersPerMatch must be between 2 and 16", StatusCodes.Status400BadRequest);
        }

        var userId = GetUserId(context);
        var (ticket, conflict) = await matchmakingService.EnqueueAsync(userId, request.Mode, request.Region, playersPerMatch, ct);
        if (conflict)
        {
            return ApiResults.Error("Already queued for this mode/region", StatusCodes.Status409Conflict);
        }

        return ApiResults.Ok(new
        {
            queued = true,
            id = ticket.Id,
            mode = ticket.Mode,
            region = ticket.Region,
            createdAt = ticket.EnqueuedAt
        });
    }

    private static async Task<IResult> CancelAsync(
        HttpContext context,
        MatchmakingService matchmakingService,
        CancellationToken ct)
    {
        var userId = GetUserId(context);
        var canceled = await matchmakingService.CancelAsync(userId, ct);
        return ApiResults.Ok(new
        {
            canceled,
            message = canceled ? "Canceled matchmaking" : "No active queue ticket"
        });
    }

    private static async Task<IResult> StatusAsync(
        HttpContext context,
        MatchmakingService matchmakingService,
        CancellationToken ct)
    {
        var userId = GetUserId(context);
        var status = await matchmakingService.GetStatusAsync(userId, ct);
        return ApiResults.Ok(status);
    }

    private static async Task<IResult> GetMatchAsync(
        string matchId,
        MatchmakingService matchmakingService,
        CancellationToken ct)
    {
        var match = await matchmakingService.GetMatchAsync(matchId, ct);
        if (match == null)
        {
            return ApiResults.Error("Match not found", StatusCodes.Status404NotFound);
        }

        return ApiResults.Ok(match);
    }

    private static string GetUserId(HttpContext context)
    {
        return context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ??
               context.User.Identity?.Name ??
               string.Empty;
    }
}
