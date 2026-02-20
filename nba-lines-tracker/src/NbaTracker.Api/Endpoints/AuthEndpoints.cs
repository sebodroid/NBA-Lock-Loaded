using Microsoft.EntityFrameworkCore;
using NbaTracker.Api.Models;
using NbaTracker.Api.Services;
using NbaTracker.Data;
using NbaTracker.Data.Entities;

namespace NbaTracker.Api.Endpoints;

public static class AuthEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest req,
        NbaTrackerDbContext db,
        TokenService tokens,
        CancellationToken ct)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == req.Email, ct);

        // Always run BCrypt.Verify — prevents timing-based email enumeration
        var hashToCheck = user?.PasswordHash ?? BCrypt.Net.BCrypt.HashPassword("dummy-timing-safe");
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, hashToCheck))
            return Results.Unauthorized();

        var accessToken = tokens.GenerateAccessToken(user);
        var refreshPlaintext = tokens.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokens.HashRefreshToken(refreshPlaintext),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new LoginResponse(accessToken, refreshPlaintext, 900));
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest req,
        NbaTrackerDbContext db,
        TokenService tokens,
        CancellationToken ct)
    {
        // Cannot query by BCrypt hash — fetch candidates by time window, verify in memory
        var candidates = await db.RefreshTokens
            .Include(rt => rt.User)
            .Where(rt => rt.ExpiresAt > DateTime.UtcNow
                      && rt.RevokedAt == null
                      && rt.CreatedAt > DateTime.UtcNow.AddDays(-8))
            .ToListAsync(ct);

        var match = candidates.FirstOrDefault(rt =>
            tokens.VerifyRefreshToken(req.RefreshToken, rt.TokenHash));

        if (match is null)
            return Results.Unauthorized();

        // Token rotation: revoke old, issue new pair
        match.RevokedAt = DateTime.UtcNow;

        var newAccessToken = tokens.GenerateAccessToken(match.User);
        var newRefreshPlaintext = tokens.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = match.UserId,
            TokenHash = tokens.HashRefreshToken(newRefreshPlaintext),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);

        return Results.Ok(new LoginResponse(newAccessToken, newRefreshPlaintext, 900));
    }

    private static async Task<IResult> LogoutAsync(
        RefreshRequest req,
        NbaTrackerDbContext db,
        TokenService tokens,
        CancellationToken ct)
    {
        var candidates = await db.RefreshTokens
            .Where(rt => rt.ExpiresAt > DateTime.UtcNow
                      && rt.RevokedAt == null
                      && rt.CreatedAt > DateTime.UtcNow.AddDays(-8))
            .ToListAsync(ct);

        var match = candidates.FirstOrDefault(rt =>
            tokens.VerifyRefreshToken(req.RefreshToken, rt.TokenHash));

        if (match is not null)
        {
            match.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        // Always return 200 — logout is idempotent (already-revoked token is not an error)
        return Results.Ok();
    }
}
