using Microsoft.EntityFrameworkCore;
using NbaTracker.Api.Models;
using NbaTracker.Data;
using NbaTracker.Data.Entities;

namespace NbaTracker.Api.Endpoints;

public static class TeamEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", GetAllTeamsAsync);
        group.MapGet("/{id:int}/stats", GetTeamStatsAsync);
        group.MapGet("/{id:int}/games", GetTeamGamesAsync);
    }

    // GET /api/{sport}/teams — all teams for a sport with aggregate ATS/OU stats.
    // Optional ?season=2025 shows a specific completed season instead of the live one.
    private static async Task<IResult> GetAllTeamsAsync(
        string sport,
        NbaTrackerDbContext db,
        CancellationToken ct,
        string? season = null)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        var teams = await db.Teams.Where(t => t.Sport == sport).ToListAsync(ct);

        // Once the season starts, preseason games drop out of stats automatically —
        // unless a specific past season was requested, which always gets its full window
        var (resolvedSeason, statsStart) = SeasonHelper.ResolveSeasonWindow(
            sport, season, DateOnly.FromDateTime(DateTime.UtcNow));

        // Load all FINAL games with results in ONE query, scoped to this sport
        // AtsResult/OuResult enum comparisons happen in C# below, not in this EF query
        var finalGames = await db.Games
            .Where(g => g.Sport == sport && g.Season == resolvedSeason
                     && g.Status == "FINAL" && g.GameDate >= statsStart)
            .Include(g => g.GameResult)
            .ToListAsync(ct);

        // Fetch the most recent completed SyncRun timestamp for this sport
        var lastSync = await db.SyncRuns
            .Where(r => r.Sport == sport && r.CompletedAt != null)
            .OrderByDescending(r => r.CompletedAt)
            .Select(r => r.CompletedAt)
            .FirstOrDefaultAsync(ct);
        string? lastSyncedAt = lastSync?.ToString("O");  // ISO 8601 round-trip format

        var stats = teams.Select(team =>
        {
            // Partition once per team — avoids repeated full-scan over finalGames
            var homeGames = finalGames.Where(g => g.HomeTeamId == team.Id).ToList();
            var awayGames = finalGames.Where(g => g.AwayTeamId == team.Id).ToList();

            int wins = homeGames.Count(g => g.HomeScore > g.AwayScore)
                     + awayGames.Count(g => g.AwayScore > g.HomeScore);
            int losses = homeGames.Count(g => g.HomeScore < g.AwayScore)
                       + awayGames.Count(g => g.AwayScore < g.HomeScore);

            // Enum comparisons in C# — safe because list is already materialized
            int atsCovers = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Cover)
                          + awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Cover);
            int atsLosses = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Loss)
                          + awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Loss);
            int atsPushes = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Push)
                          + awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Push);

            // O/U is the same result for both sides of the same game
            // Avoid double-counting: use homeGames for all games (each game appears once as home to one team)
            // Then add away games — but OuResult is per-game, so union both sides safely
            var allGames = homeGames.Concat(awayGames).ToList();
            int ouOvers = allGames.Count(g => g.GameResult?.OuResult == OuResult.Over);
            int ouUnders = allGames.Count(g => g.GameResult?.OuResult == OuResult.Under);
            int ouPushes = allGames.Count(g => g.GameResult?.OuResult == OuResult.Push);

            // Compute streak: walk all team games sorted descending by GameDate
            var allTeamGames = homeGames.Concat(awayGames)
                .OrderByDescending(g => g.GameDate)
                .ToList();

            int streak = 0;
            if (allTeamGames.Count > 0)
            {
                // Determine win/loss for most recent game
                var first = allTeamGames[0];
                bool firstWon = (first.HomeTeamId == team.Id && first.HomeScore > first.AwayScore)
                             || (first.AwayTeamId == team.Id && first.AwayScore > first.HomeScore);

                // Walk games counting consecutive same result — stop at first different result
                foreach (var g in allTeamGames)
                {
                    bool won = (g.HomeTeamId == team.Id && g.HomeScore > g.AwayScore)
                            || (g.AwayTeamId == team.Id && g.AwayScore > g.HomeScore);
                    if (won == firstWon)
                        streak += firstWon ? 1 : -1;
                    else
                        break;
                }
            }

            return new TeamStatsResponse(
                team.Id, team.Name, team.Abbreviation, team.Conference, team.Division,
                homeGames.Count + awayGames.Count,
                wins, losses,
                atsCovers, atsLosses, atsPushes,
                ouOvers, ouUnders, ouPushes,
                streak, lastSyncedAt
            );
        }).ToList();

        return Results.Ok(stats);
    }

    // GET /api/{sport}/teams/{id}/stats — home/away splits for a single team
    private static async Task<IResult> GetTeamStatsAsync(
        string sport,
        int id,
        NbaTrackerDbContext db,
        CancellationToken ct,
        string? season = null)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == id && t.Sport == sport, ct);
        if (team is null) return Results.NotFound();

        var (resolvedSeason, statsStart) = SeasonHelper.ResolveSeasonWindow(
            sport, season, DateOnly.FromDateTime(DateTime.UtcNow));

        // Two targeted queries (one for home games, one for away) — avoids loading all rows
        var homeGames = await db.Games
            .Where(g => g.HomeTeamId == id && g.Sport == sport && g.Season == resolvedSeason
                     && g.Status == "FINAL" && g.GameDate >= statsStart)
            .Include(g => g.GameResult)
            .ToListAsync(ct);

        var awayGames = await db.Games
            .Where(g => g.AwayTeamId == id && g.Sport == sport && g.Season == resolvedSeason
                     && g.Status == "FINAL" && g.GameDate >= statsStart)
            .Include(g => g.GameResult)
            .ToListAsync(ct);

        // All enum comparisons in C# after ToListAsync
        var home = BuildSplit(homeGames, isHome: true);
        var away = BuildSplit(awayGames, isHome: false);

        return Results.Ok(new TeamDetailResponse(
            team.Id, team.Name, team.Abbreviation,
            team.Conference, team.Division,
            home, away
        ));
    }

    // GET /api/{sport}/teams/{id}/games — game log for a single team
    private static async Task<IResult> GetTeamGamesAsync(
        string sport,
        int id,
        NbaTrackerDbContext db,
        CancellationToken ct,
        string? season = null)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        var exists = await db.Teams.AnyAsync(t => t.Id == id && t.Sport == sport, ct);
        if (!exists) return Results.NotFound();

        var (resolvedSeason, statsStart) = SeasonHelper.ResolveSeasonWindow(
            sport, season, DateOnly.FromDateTime(DateTime.UtcNow));

        // Load game details — Include navigation properties needed for the DTO projection
        // Do NOT project AtsResult/OuResult enums in the LINQ Select — materialize first, project in C#
        var games = await db.Games
            .Where(g => (g.HomeTeamId == id || g.AwayTeamId == id)
                     && g.Sport == sport && g.Season == resolvedSeason
                     && g.Status == "FINAL" && g.GameDate >= statsStart)
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Include(g => g.GameLine)
            .Include(g => g.GameResult)
            .OrderByDescending(g => g.GameDate)
            .ToListAsync(ct);

        // Project to DTOs in C# — enum .ToString() is safe here
        var log = games.Select(g =>
        {
            bool isHome = g.HomeTeamId == id;
            var atsResult = isHome
                ? g.GameResult?.HomeAtsResult?.ToString()
                : g.GameResult?.AwayAtsResult?.ToString();

            return new GameLogEntry(
                g.Id,
                g.GameDate,
                g.HomeTeam.Abbreviation,
                g.AwayTeam.Abbreviation,
                g.HomeScore,
                g.AwayScore,
                isHome,
                g.GameLine?.Spread,
                g.GameLine?.Total,
                atsResult,
                g.GameResult?.OuResult?.ToString()
            );
        }).ToList();

        return Results.Ok(log);
    }

    private static HomeAwaySplit BuildSplit(List<Game> games, bool isHome)
    {
        int wins = isHome
            ? games.Count(g => g.HomeScore > g.AwayScore)
            : games.Count(g => g.AwayScore > g.HomeScore);
        int losses = isHome
            ? games.Count(g => g.HomeScore < g.AwayScore)
            : games.Count(g => g.AwayScore < g.HomeScore);

        int atsCovers = isHome
            ? games.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Cover)
            : games.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Cover);
        int atsLosses = isHome
            ? games.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Loss)
            : games.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Loss);
        int atsPushes = isHome
            ? games.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Push)
            : games.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Push);

        int ouOvers = games.Count(g => g.GameResult?.OuResult == OuResult.Over);
        int ouUnders = games.Count(g => g.GameResult?.OuResult == OuResult.Under);
        int ouPushes = games.Count(g => g.GameResult?.OuResult == OuResult.Push);

        return new HomeAwaySplit(
            games.Count, wins, losses,
            atsCovers, atsLosses, atsPushes,
            ouOvers, ouUnders, ouPushes
        );
    }
}
