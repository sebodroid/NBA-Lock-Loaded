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
        group.MapGet("/debug/stat-names", GetStatNamesAsync);
        group.MapGet("/debug/games", GetDebugGamesAsync);
    }

    // GET /api/admin/debug/games?date=2026-09-14&sport=NFL — recent games with their
    // status, score, and how many PlayerGameStat rows exist for them, so a "why won't
    // this bet grade" question can be answered directly: 0 box-score rows on a FINAL
    // game means the box-score fetch hasn't succeeded yet (check Worker logs for that
    // match), not that grading itself is broken.
    private static async Task<IResult> GetDebugGamesAsync(
        NbaTrackerDbContext db,
        CancellationToken ct,
        string? date = null,
        string sport = "NFL")
    {
        var query = db.Games.Include(g => g.HomeTeam).Include(g => g.AwayTeam)
            .Where(g => g.Sport == sport);

        if (date is not null && DateOnly.TryParse(date, out var d))
            query = query.Where(g => g.GameDate == d);

        var games = await query.OrderByDescending(g => g.GameDate).Take(50).ToListAsync(ct);
        var gameIds = games.Select(g => g.Id).ToList();

        var statCounts = await db.PlayerGameStats
            .Where(s => gameIds.Contains(s.GameId))
            .GroupBy(s => s.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GameId, x => x.Count, ct);

        return Results.Ok(games.Select(g => new
        {
            g.Id,
            GameDate = g.GameDate.ToString("yyyy-MM-dd"),
            g.Status,
            g.HomeScore,
            g.AwayScore,
            Matchup = $"{g.AwayTeam.Abbreviation} @ {g.HomeTeam.Abbreviation}",
            BoxScoreRows = statCounts.GetValueOrDefault(g.Id, 0)
        }));
    }

    // GET /api/admin/debug/stat-names — every distinct (Category, StatName) pair captured
    // in PlayerGameStats, with a count. Exists to verify the keyword guesses in
    // PropMarketMapping against Highlightly's real stat-name vocabulary without needing
    // direct DB/psql access — hit this once real box scores exist and compare against
    // PropMarketMapping.cs's Keyword/Exclude choices.
    private static async Task<IResult> GetStatNamesAsync(
        NbaTrackerDbContext db,
        CancellationToken ct)
    {
        var rows = await db.PlayerGameStats
            .GroupBy(s => new { s.Category, s.StatName })
            .Select(g => new { g.Key.Category, g.Key.StatName, Rows = g.Count() })
            .OrderBy(r => r.Category).ThenBy(r => r.StatName)
            .ToListAsync(ct);

        return Results.Ok(rows);
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
