using Microsoft.EntityFrameworkCore;
using NbaTracker.Api.Models;
using NbaTracker.Data;
using NbaTracker.Data.Entities;

namespace NbaTracker.Api.Endpoints;

public static class AdminEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/users", CreateUserAsync);
        group.MapGet("/sync-status", GetSyncStatusAsync);
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest req,
        NbaTrackerDbContext db,
        CancellationToken ct)
    {
        var exists = await db.Users.AnyAsync(u => u.Email == req.Email, ct);
        if (exists)
            return Results.Conflict(new { error = "Email already registered" });

        db.Users.Add(new User
        {
            Email = req.Email,
            Username = req.Email,   // default display name to email until Phase 4 adds a username field
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            IsAdmin = req.IsAdmin,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/admin/users", new { email = req.Email });
    }

    private static async Task<IResult> GetSyncStatusAsync(
        NbaTrackerDbContext db,
        CancellationToken ct)
    {
        var recent = await db.SyncRuns
            .OrderByDescending(r => r.StartedAt)
            .Take(10)
            .ToListAsync(ct);

        return Results.Ok(recent.Select(r => new
        {
            r.Id,
            r.StartedAt,
            r.CompletedAt,
            Status = r.Status.ToString(),
            r.GamesProcessed,
            r.ErrorDetails
        }));
    }
}
