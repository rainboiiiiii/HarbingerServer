using System.IdentityModel.Tokens.Jwt;
using GameBackend.Api.Models;
using GameBackend.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace GameBackend.Api.Endpoints;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/inventory").RequireAuthorization().WithOpenApi();

        group.MapPost("/purchase", PurchaseAsync);
    }

    private static async Task<IResult> PurchaseAsync(
        PurchaseRequest request,
        HttpContext context,
        ProgressionService progressionService,
        CancellationToken ct)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        try
        {
            var response = await progressionService.PurchaseItemAsync(userId, request.ItemId, request.Currency, request.Cost, ct);
            return ApiResults.Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResults.Error(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            return ApiResults.Error(ex.Message, StatusCodes.Status500InternalServerError);
        }
    }

    private static string GetUserId(HttpContext context)
    {
        return context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ??
               context.User.Identity?.Name ??
               string.Empty;
    }
}
