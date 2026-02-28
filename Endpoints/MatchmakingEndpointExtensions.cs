using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;

namespace GameBackend.Api.Endpoints;

public static class MatchmakingEndpointExtensions
{
    public static void MapMatchmakingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/matchmaking").RequireAuthorization();

        group.MapPost("/enqueue", MatchmakingEndpoints.EnqueueAsync).WithOpenApi();
        group.MapPost("/cancel", MatchmakingEndpoints.CancelAsync).WithOpenApi();
        group.MapGet("/status", MatchmakingEndpoints.StatusAsync).WithOpenApi();
        group.MapGet("/match/{matchId}", MatchmakingEndpoints.GetMatchAsync).WithOpenApi();
    }
}
