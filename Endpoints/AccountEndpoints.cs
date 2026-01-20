using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GameBackend.Api.Auth;
using GameBackend.Api.Data;
using GameBackend.Api.Models;
using GameBackend.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GameBackend.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/accounts");

        group.MapPost("/signup", SignupAsync).AllowAnonymous().WithOpenApi();
        group.MapPost("/login", LoginAsync).AllowAnonymous().WithOpenApi();
        group.MapGet("/me", MeAsync).RequireAuthorization().WithOpenApi();
    }

    private static async Task<IResult> SignupAsync(
        SignupRequest request,
        MongoDbContext db,
        PasswordService passwordService,
        JwtTokenService jwtTokenService,
        ProgressionService progressionService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ApiResults.Error("Username and password are required", StatusCodes.Status400BadRequest);
        }

        if (request.Username.Length is < 3 or > 20)
        {
            return ApiResults.Error("Username must be 3-20 characters", StatusCodes.Status400BadRequest);
        }

        if (request.Password.Length is < 8 or > 64)
        {
            return ApiResults.Error("Password must be 8-64 characters", StatusCodes.Status400BadRequest);
        }

        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var existing = await db.Users.Find(u => u.UsernameNormalized == normalizedUsername).FirstOrDefaultAsync(ct);
        if (existing != null)
        {
            return ApiResults.Error("Username already exists", StatusCodes.Status409Conflict);
        }

        var user = new User
        {
            Username = request.Username.Trim(),
            UsernameNormalized = normalizedUsername,
            PasswordHash = passwordService.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
            LastLogin = DateTime.UtcNow
        };

        try
        {
            await db.Users.InsertOneAsync(user, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            logger.LogWarning("Duplicate username attempted for {Username}", normalizedUsername);
            return ApiResults.Error("Username already exists", StatusCodes.Status409Conflict);
        }

        await progressionService.GetProgressionAsync(user.Id, ct);

        var (token, expiresAt) = jwtTokenService.GenerateToken(user);
        return ApiResults.Ok(new AuthResponse(user.Id, user.Username, token, expiresAt));
    }

    private static async Task<IResult> LoginAsync(
        HttpContext httpContext,
        LoginRequest request,
        MongoDbContext db,
        PasswordService passwordService,
        JwtTokenService jwtTokenService,
        LoginRateLimiter rateLimiter,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ApiResults.Error("Username and password are required", StatusCodes.Status400BadRequest);
        }

        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var rateKey = $"{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}:{normalizedUsername}";

        if (!rateLimiter.AllowAttempt(rateKey, out var retryAfter))
        {
            var retrySeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            return ApiResults.Error($"Too many login attempts. Try again in {retrySeconds} seconds.", StatusCodes.Status429TooManyRequests);
        }

        var user = await db.Users.Find(u => u.UsernameNormalized == normalizedUsername).FirstOrDefaultAsync(ct);
        if (user == null || !passwordService.Verify(request.Password, user.PasswordHash))
        {
            return ApiResults.Error("Invalid username or password", StatusCodes.Status401Unauthorized);
        }

        var update = Builders<User>.Update.Set(u => u.LastLogin, DateTime.UtcNow);
        await db.Users.UpdateOneAsync(u => u.Id == user.Id, update, cancellationToken: ct);

        var (token, expiresAt) = jwtTokenService.GenerateToken(user);
        logger.LogInformation("User {UserId} logged in", user.Id);
        return ApiResults.Ok(new AuthResponse(user.Id, user.Username, token, expiresAt));
    }

    [Authorize]
    private static async Task<IResult> MeAsync(
        HttpContext context,
        ProgressionService progressionService,
        CancellationToken ct)
    {
        var userId = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                     context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                     string.Empty;
        var username = context.User.Identity?.Name ??
                       context.User.FindFirstValue(JwtRegisteredClaimNames.UniqueName) ??
                       context.User.FindFirstValue(ClaimTypes.Name) ??
                       string.Empty;
        var expClaim = context.User.FindFirst("exp")?.Value;

        DateTime expiresAt = DateTime.UtcNow;
        if (long.TryParse(expClaim, out var expSeconds))
        {
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
        }

        var progression = await progressionService.GetProgressionAsync(userId, ct);

        return ApiResults.Ok(new MeResponse(
            userId,
            username,
            expiresAt,
            progression.Dust,
            progression.Crystals,
            progression.Inventory,
            progression
        ));
    }
}
