using System.IdentityModel.Tokens.Jwt;
using GameBackend.Api.Models;
using GameBackend.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace GameBackend.Api.Endpoints;

public static class MatchReportEndpoints
{
    public static void MapMatchReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/match").RequireAuthorization();
        group.MapPost("/report", ReportAsync).WithOpenApi();
    }

    private static async Task<IResult> ReportAsync(
        HttpContext context,
        MatchReportRequest request,
        MatchReportService matchReportService,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.MatchId) || string.IsNullOrWhiteSpace(request.HostId))
        {
            return ApiResults.Error("matchId and hostId are required", StatusCodes.Status400BadRequest);
        }

        if (request.PlayerSummaries == null || request.PlayerSummaries.Count == 0)
        {
            return ApiResults.Error("playerSummaries is required", StatusCodes.Status400BadRequest);
        }

        var userId = GetUserId(context);

        try
        {
            var (match, awards) = await matchReportService.ReportMatchAsync(userId, request, ct);
            return ApiResults.Ok(new
            {
                matchId = match.Id,
                awards = awards.Select(a => new
                {
                    userId = a.UserId,
                    xpAwarded = a.XpAwarded,
                    newXp = a.NewXp,
                    newLevel = a.NewLevel
                }).ToArray()
            });
        }
        catch (InvalidOperationException ex)
        {
            var message = ex.Message == "Match not found" ? "Match not found" : "Invalid match data";
            var status = ex.Message == "Match not found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest;
            return ApiResults.Error(message, status);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ApiResults.Error(ex.Message, StatusCodes.Status403Forbidden);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return ApiResults.Error(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static string GetUserId(HttpContext context)
    {
        return context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ??
               context.User.Identity?.Name ??
               string.Empty;
    }
}
