using System.IdentityModel.Tokens.Jwt;
using GameBackend.Api.Models;
using GameBackend.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace GameBackend.Api.Endpoints;

public class MatchmakingEndpoints
{
    public static async Task<IResult> EnqueueAsync(
        HttpContext context,
        EnqueueRequest request,
        MatchmakingService matchmakingService,
        ILogger<MatchmakingEndpoints> logger,
        CancellationToken ct)
    {
        logger.LogInformation("Enqueue request received from user {UserId} for mode {Mode}, region {Region}.", GetUserId(context), request?.Mode, request?.Region);

        if (request is null || string.IsNullOrWhiteSpace(request.Mode) || string.IsNullOrWhiteSpace(request.Region))
        {
            logger.LogError("Enqueue request failed: mode and/or region are missing.");
            return ApiResults.Error("mode and region are required", StatusCodes.Status400BadRequest);
        }

        var playersPerMatch = request.PlayersPerMatch <= 0 ? 4 : request.PlayersPerMatch;
        if (playersPerMatch < 2 || playersPerMatch > 16)
        {
            logger.LogError("Enqueue request failed: playersPerMatch must be between 2 and 16, but was {PlayersPerMatch}.", playersPerMatch);
            return ApiResults.Error("playersPerMatch must be between 2 and 16", StatusCodes.Status400BadRequest);
        }

        var userId = GetUserId(context);
        var (ticket, conflict) = await matchmakingService.EnqueueAsync(userId, request.Mode, request.Region, playersPerMatch, request.EquippedWeapons, request.EquippedAbilities, ct);
        if (conflict)
        {
            logger.LogWarning("User {UserId} attempted to enqueue but is already queued for mode {Mode}, region {Region}.", userId, request.Mode, request.Region);
            return ApiResults.Error("Already queued for this mode/region", StatusCodes.Status409Conflict);
        }

        logger.LogInformation("User {UserId} successfully enqueued with ticket {TicketId}.", userId, ticket.Id);
        return ApiResults.Ok(new
        {
            queued = true,
            id = ticket.Id,
            mode = ticket.Mode,
            region = ticket.Region,
            createdAt = ticket.EnqueuedAt
        });
    }

    public static async Task<IResult> CancelAsync(
        HttpContext context,
        MatchmakingService matchmakingService,
        ILogger<MatchmakingEndpoints> logger,
        CancellationToken ct)
    {
        var userId = GetUserId(context);
        logger.LogInformation("Cancel request received from user {UserId}.", userId);
        var canceled = await matchmakingService.CancelAsync(userId, ct);
        if (canceled)
        {
            logger.LogInformation("User {UserId} successfully canceled matchmaking.", userId);
        }
        else
        {
            logger.LogInformation("User {UserId} attempted to cancel but had no active queue ticket.", userId);
        }
        return ApiResults.Ok(new
        {
            canceled,
            message = canceled ? "Canceled matchmaking" : "No active queue ticket"
        });
    }

    public static async Task<IResult> StatusAsync(
        HttpContext context,
        MatchmakingService matchmakingService,
        ILogger<MatchmakingEndpoints> logger,
        CancellationToken ct)
    {
        var userId = GetUserId(context);
        logger.LogInformation("Status request received from user {UserId}.", userId);
        var status = await matchmakingService.GetStatusAsync(userId, ct);
        logger.LogInformation("Returning status {Status} for user {UserId}.", status.Status, userId);
        return ApiResults.Ok(status);
    }

    public static async Task<IResult> GetMatchAsync(
        string matchId,
        MatchmakingService matchmakingService,
        ILogger<MatchmakingEndpoints> logger,
        CancellationToken ct)
    {
        logger.LogInformation("GetMatch request received for match {MatchId}.", matchId);
        var match = await matchmakingService.GetMatchAsync(matchId, ct);
        if (match == null)
        {
            logger.LogWarning("Match {MatchId} not found for GetMatch request.", matchId);
            return ApiResults.Error("Match not found", StatusCodes.Status404NotFound);
        }

        logger.LogInformation("Returning match {MatchId} details.", matchId);
        return ApiResults.Ok(match);
    }

    private static string GetUserId(HttpContext context)
    {
        return context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
               context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ??
               context.User.Identity?.Name ??
               string.Empty;
    }
}
