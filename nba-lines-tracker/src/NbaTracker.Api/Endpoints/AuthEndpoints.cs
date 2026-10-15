using System.Security.Cryptography;
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
        group.MapPost("/register", RegisterAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
        group.MapPost("/forgot-password", ForgotPasswordAsync);
        group.MapPost("/reset-password", ResetPasswordAsync);
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

        // Correct credentials, but an admin hasn't let this account in yet — distinct from
        // "wrong password" so the frontend can show an honest message instead of implying
        // a typo.
        if (!user.IsApproved)
            return Results.Json(new { error = "pending_approval" }, statusCode: StatusCodes.Status403Forbidden);

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

        return Results.Ok(new LoginResponse(accessToken, refreshPlaintext, 900, user.IsAdmin));
    }

    // POST /api/auth/register — creates the account but does NOT log them in: IsApproved
    // starts false, and LoginAsync rejects any unapproved account regardless of a correct
    // password. An admin has to explicitly approve via AdminEndpoints first.
    private static async Task<IResult> RegisterAsync(
        RegisterRequest req,
        NbaTrackerDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Username)
            || string.IsNullOrWhiteSpace(req.Password))
            return Results.BadRequest(new { error = "Email, username, and password are required." });

        if (req.Password.Length < 8)
            return Results.BadRequest(new { error = "Password must be at least 8 characters." });

        var exists = await db.Users.AnyAsync(u => u.Email == req.Email, ct);
        if (exists)
            return Results.Conflict(new { error = "An account with that email already exists." });

        db.Users.Add(new User
        {
            Email = req.Email,
            Username = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            IsAdmin = false,
            IsApproved = false,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Results.Ok(new { message = "Account created — waiting for approval." });
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

        return Results.Ok(new LoginResponse(newAccessToken, newRefreshPlaintext, 900, match.User.IsAdmin));
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

    // POST /api/auth/forgot-password — always returns the same generic 200, whether or
    // not the email matches an account, so this endpoint can't be used to enumerate
    // which emails have accounts.
    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest req,
        NbaTrackerDbContext db,
        EmailService email,
        IConfiguration config,
        CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email, ct);
        if (user is not null)
        {
            var plaintext = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

            db.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = BCrypt.Net.BCrypt.HashPassword(plaintext),
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);

            var baseUrl = (config["Frontend:BaseUrl"] ?? "").TrimEnd('/');
            var resetUrl = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(plaintext)}";

            try
            {
                await email.SendPasswordResetEmailAsync(user.Email, resetUrl, ct);
            }
            catch
            {
                // Don't leak email-delivery failures to the caller — would reveal whether
                // the account exists just as surely as a different status code would.
            }
        }

        return Results.Ok(new { message = "If that email has an account, a reset link has been sent." });
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest req,
        NbaTrackerDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
            return Results.BadRequest(new { error = "Password must be at least 8 characters." });

        // Cannot query by BCrypt hash — fetch unused/unexpired candidates, verify in memory
        var candidates = await db.PasswordResetTokens
            .Include(t => t.User)
            .Where(t => t.ExpiresAt > DateTime.UtcNow && t.UsedAt == null)
            .ToListAsync(ct);

        var match = candidates.FirstOrDefault(t =>
            BCrypt.Net.BCrypt.Verify(req.Token, t.TokenHash));

        if (match is null)
            return Results.BadRequest(new { error = "This reset link is invalid or has expired." });

        match.UsedAt = DateTime.UtcNow;
        match.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);

        // Revoke every existing refresh token — a password reset should sign the account
        // out everywhere, not just leave old sessions valid under the new password.
        var activeRefreshTokens = await db.RefreshTokens
            .Where(rt => rt.UserId == match.UserId && rt.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var rt in activeRefreshTokens)
            rt.RevokedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Results.Ok(new { message = "Password has been reset." });
    }
}
