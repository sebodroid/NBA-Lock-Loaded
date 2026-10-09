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
        group.MapGet("/recent", GetRecentGamesAsync);
        group.MapGet("/{id:int}/preview", GetPreviewAsync);
        group.MapGet("/{id:int}/props", GetGamePropsAsync);
        group.MapGet("/{id:int}/players", GetGamePlayersAsync);
    }

    // GET /api/{sport}/games/recent?days=10 — a lightweight game list spanning the last
    // `days` days through the next `days`, for the manual-bet game picker (a bet might be
    // for a game already played — logging one made elsewhere after the fact — or one
    // still upcoming).
    private static async Task<IResult> GetRecentGamesAsync(
        string sport, NbaTrackerDbContext db, CancellationToken ct, int days = 10)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        days = Math.Clamp(days, 1, 30);
        var today = ApiClock.Today;
        var start = today.AddDays(-days);
        var end = today.AddDays(days);

        var games = await db.Games
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Where(g => g.Sport == sport && g.GameDate >= start && g.GameDate <= end && g.Status != "POSTPONED")
            .OrderByDescending(g => g.GameDate)
            .ToListAsync(ct);

        return Results.Ok(games.Select(g => new GameListEntry(
            g.Id, g.GameDate.ToString("yyyy-MM-dd"), g.Status,
            g.HomeTeamId, g.HomeTeam.Abbreviation, g.HomeTeam.Name,
            g.AwayTeamId, g.AwayTeam.Abbreviation, g.AwayTeam.Name
        )));
    }

    // GET /api/{sport}/games/{id}/players — every player on either roster for this game
    // (last known team matches one of the two sides), for the manual player-prop picker.
    private static async Task<IResult> GetGamePlayersAsync(
        string sport, int id, NbaTrackerDbContext db, CancellationToken ct)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == id && g.Sport == sport, ct);
        if (game is null) return Results.NotFound();

        var players = await db.Players
            .Include(p => p.Team)
            .Where(p => p.Sport == sport && (p.TeamId == game.HomeTeamId || p.TeamId == game.AwayTeamId))
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return Results.Ok(players.Select(p => new GamePlayerEntry(p.Id, p.Name, p.Team?.Abbreviation)));
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

        var statsStart = SeasonHelper.StatsStartDate(sport, ApiClock.Today);
        var injuries = await InjuryLookup.GetForPlayersAsync(db, propLines.Select(l => l.PlayerId), ct);

        // Shared across the loop below — a game's props are almost all the same handful of
        // markets (Passing/Rushing/Receiving Yards, etc.) repeated across many players, so
        // the league-wide side of each estimate only needs computing once per market.
        var teamsById = await db.Teams.Where(t => t.Sport == sport).ToDictionaryAsync(t => t.Id, ct);
        var leagueCache = new Dictionary<string, PlayerEndpoints.LeagueAllowedContext>();

        var entries = new List<GamePropEntry>();
        foreach (var line in propLines)
        {
            var estimate = await PlayerEndpoints.BuildPropEstimateAsync(
                line.Player, line, statsStart, db, ct, teamsById, leagueCache);
            injuries.TryGetValue(line.Player.Id, out var injury);
            entries.Add(new GamePropEntry(
                line.Player.Id, line.Player.Name, line.Player.Team?.Abbreviation, estimate,
                injury.Status, injury.Note));
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

        var today = ApiClock.Today;

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
        var priorSeason = SeasonHelper.PriorCompletedSeason(sport, today);

        var seasonGames = await db.Games
            .Include(g => g.GameLine)
            .Include(g => g.GameResult)
            .Where(g => g.Sport == sport
                     && g.Season == season
                     && g.Status == "FINAL"
                     && g.GameDate >= statsStart
                     && (teamIds.Contains(g.HomeTeamId) || teamIds.Contains(g.AwayTeamId)))
            .ToListAsync(ct);

        // Head-to-head spans this season plus the last completed one (when we have it
        // backfilled) — two teams that haven't played each other yet this year can still
        // show last season's meetings, rather than "no matchups" when the data exists.
        // Not filtered by statsStart for the prior season: the historical backfill only
        // ever captured real regular-season/playoff dates to begin with, so there's no
        // stray preseason noise to exclude there.
        var h2hGames = priorSeason is null
            ? seasonGames
            : await db.Games
                .Include(g => g.GameLine)
                .Include(g => g.GameResult)
                .Where(g => g.Sport == sport
                         && g.Status == "FINAL"
                         && (teamIds.Contains(g.HomeTeamId) || teamIds.Contains(g.AwayTeamId))
                         && ((g.Season == season && g.GameDate >= statsStart) || g.Season == priorSeason))
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

            // Head-to-head: all FINAL games between these two teams, this season plus the
            // prior one, newest first
            var h2hEntries = h2hGames
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
                h2hEntries
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
