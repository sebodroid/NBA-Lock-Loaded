using Microsoft.EntityFrameworkCore;
using NbaTracker.Api.Models;
using NbaTracker.Api.Services;
using NbaTracker.Data;
using NbaTracker.Data.Entities;

namespace NbaTracker.Api.Endpoints;

public static class GameEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/today", GetTodayMatchupsAsync);
        group.MapGet("/{id:int}/preview", GetPreviewAsync);
        group.MapGet("/{id:int}/props", GetGamePropsAsync);
    }

    // GET /api/{sport}/games/{id}/props — every player prop line tied to this game,
    // each with the same hit-rate/opponent-adjusted estimate the player card shows.
    // The direct "what can I bet on for this game" view — doesn't require the player
    // to already appear in a leaderboard (they won't yet, before their first game
    // of a new season).
    private static async Task<IResult> GetGamePropsAsync(
        string sport,
        int id,
        NbaTrackerDbContext db,
        CancellationToken ct)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == id && g.Sport == sport, ct);
        if (game is null) return Results.NotFound();

        var propLines = await db.PlayerPropLines
            .Include(l => l.Player).ThenInclude(p => p.Team)
            .Include(l => l.Game)
            .Where(l => l.GameId == id)
            .OrderBy(l => l.Player.Name)
            .ToListAsync(ct);

        var statsStart = SeasonHelper.StatsStartDate(sport, DateOnly.FromDateTime(DateTime.UtcNow));

        var entries = new List<GamePropEntry>();
        foreach (var line in propLines)
        {
            var estimate = await PlayerEndpoints.BuildPropEstimateAsync(line.Player, line, statsStart, db, ct);
            entries.Add(new GamePropEntry(line.Player.Id, line.Player.Name, line.Player.Team?.Abbreviation, estimate));
        }

        return Results.Ok(entries);
    }

    // GET /api/{sport}/games/{id}/preview — Claude-generated preview, cached until stale
    private static async Task<IResult> GetPreviewAsync(
        string sport,
        int id,
        NbaTrackerDbContext db,
        AiPreviewService previewService,
        CancellationToken ct)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == id && g.Sport == sport, ct);
        if (game is null) return Results.NotFound();

        try
        {
            var text = await previewService.GetOrGeneratePreviewAsync(game, ct);
            return Results.Ok(new { text });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Could not generate preview: {ex.Message}", statusCode: 502);
        }
    }

    // GET /api/{sport}/games/today — today's games with season stats and H2H history for each matchup
    private static async Task<IResult> GetTodayMatchupsAsync(
        string sport,
        NbaTrackerDbContext db,
        CancellationToken ct)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Today's games (all statuses except POSTPONED)
        var todayGames = await db.Games
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Include(g => g.GameLine)
            .Include(g => g.GameResult)
            .Where(g => g.Sport == sport && g.GameDate == today && g.Status != "POSTPONED")
            .ToListAsync(ct);

        if (todayGames.Count == 0)
            return Results.Ok(new List<TodayMatchupResponse>());

        // Collect all unique team IDs involved in today's games
        var teamIds = todayGames
            .SelectMany(g => new[] { g.HomeTeamId, g.AwayTeamId })
            .Distinct()
            .ToHashSet();

        // Load all FINAL games for these teams this season in one query
        string season = SeasonHelper.CurrentSeason(sport, today);
        var statsStart = SeasonHelper.StatsStartDate(sport, today);

        var seasonGames = await db.Games
            .Include(g => g.GameLine)
            .Include(g => g.GameResult)
            .Where(g => g.Sport == sport
                     && g.Season == season
                     && g.Status == "FINAL"
                     && g.GameDate >= statsStart
                     && (teamIds.Contains(g.HomeTeamId) || teamIds.Contains(g.AwayTeamId)))
            .ToListAsync(ct);

        var result = todayGames.Select(game =>
        {
            int homeId = game.HomeTeamId;
            int awayId = game.AwayTeamId;

            // Season stats for home team
            var homeSeasonGames = seasonGames
                .Where(g => g.HomeTeamId == homeId || g.AwayTeamId == homeId)
                .ToList();
            var homeStats = BuildSeasonStats(homeSeasonGames, homeId);

            // Season stats for away team
            var awaySeasonGames = seasonGames
                .Where(g => g.HomeTeamId == awayId || g.AwayTeamId == awayId)
                .ToList();
            var awayStats = BuildSeasonStats(awaySeasonGames, awayId);

            // Head-to-head: all FINAL games between these two teams this season, newest first
            var h2hGames = seasonGames
                .Where(g => (g.HomeTeamId == homeId && g.AwayTeamId == awayId)
                         || (g.HomeTeamId == awayId && g.AwayTeamId == homeId))
                .OrderByDescending(g => g.GameDate)
                .Select(g => new H2HGameEntry(
                    g.Id,
                    g.GameDate.ToString("yyyy-MM-dd"),
                    g.HomeTeamId,
                    g.HomeScore,
                    g.AwayScore,
                    g.GameLine?.Spread,
                    g.GameLine?.Total,
                    g.GameResult?.HomeAtsResult?.ToString(),
                    g.GameResult?.AwayAtsResult?.ToString(),
                    g.GameResult?.OuResult?.ToString()
                ))
                .ToList();

            return new TodayMatchupResponse(
                game.Id,
                game.Status,
                homeId,
                game.HomeTeam.Name,
                game.HomeTeam.Abbreviation,
                awayId,
                game.AwayTeam.Name,
                game.AwayTeam.Abbreviation,
                game.GameLine?.Spread,
                game.GameLine?.FavoriteTeamId,
                game.GameLine?.HomeSpreadOdds,
                game.GameLine?.AwaySpreadOdds,
                game.GameLine?.Total,
                game.GameLine?.OverOdds,
                game.GameLine?.UnderOdds,
                game.GameLine?.Bookmaker,
                homeStats,
                awayStats,
                h2hGames
            );
        }).ToList();

        return Results.Ok(result);
    }

    private static TeamSeasonStats BuildSeasonStats(List<Game> games, int teamId)
    {
        var homeGames = games.Where(g => g.HomeTeamId == teamId).ToList();
        var awayGames = games.Where(g => g.AwayTeamId == teamId).ToList();

        int wins = homeGames.Count(g => g.HomeScore > g.AwayScore)
                 + awayGames.Count(g => g.AwayScore > g.HomeScore);
        int losses = homeGames.Count(g => g.HomeScore < g.AwayScore)
                   + awayGames.Count(g => g.AwayScore < g.HomeScore);

        int atsCovers = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Cover)
                      + awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Cover);
        int atsLosses = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Loss)
                      + awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Loss);
        int atsPushes = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Push)
                      + awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Push);

        // O/U is per-game (same result for both teams) — union all games, count once each
        var allGames = homeGames.Concat(awayGames).ToList();
        int ouOvers   = allGames.Count(g => g.GameResult?.OuResult == OuResult.Over);
        int ouUnders  = allGames.Count(g => g.GameResult?.OuResult == OuResult.Under);
        int ouPushes  = allGames.Count(g => g.GameResult?.OuResult == OuResult.Push);

        return new TeamSeasonStats(
            homeGames.Count + awayGames.Count,
            wins, losses,
            atsCovers, atsLosses, atsPushes,
            ouOvers, ouUnders, ouPushes
        );
    }
}
