using Microsoft.EntityFrameworkCore;
using NbaTracker.Api.Models;
using NbaTracker.Data;
using NbaTracker.Data.Entities;

namespace NbaTracker.Api.Endpoints;

public static class PlayerEndpoints
{
    // Known Highlightly stat groups — see NflStatsClient.GetBoxScoreAsync
    private static readonly HashSet<string> Categories =
        new(StringComparer.OrdinalIgnoreCase) { "Passing", "Rushing", "Receiving", "Defense", "Kicking", "Punting" };

    // Internal, not private — HotBetsEndpoints uses the same sample-size floor when
    // scanning every upcoming prop for the week's best bets.
    internal const int MinGamesForEstimate = 2;

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{category}", GetLeaderboardAsync);
    }

    public static void MapPlayerCard(RouteGroupBuilder group)
    {
        group.MapGet("/{id:int}", GetPlayerCardAsync);
    }

    // GET /api/{sport}/players/{id} — player bio, season stats, and upcoming prop lines
    // each with a player-history hit rate plus an opponent-adjusted estimate.
    private static async Task<IResult> GetPlayerCardAsync(
        string sport,
        int id,
        NbaTrackerDbContext db,
        CancellationToken ct)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        var player = await db.Players
            .Include(p => p.Team)
            .FirstOrDefaultAsync(p => p.Id == id && p.Sport == sport, ct);
        if (player is null) return Results.NotFound();

        var today = ApiClock.Today;
        var statsStart = SeasonHelper.StatsStartDate(sport, today);

        var seasonRows = await db.PlayerGameStats
            .Where(s => s.PlayerId == id && s.Game.GameDate >= statsStart)
            .ToListAsync(ct);

        var seasonStats = seasonRows
            .Where(s => s.Value.HasValue)
            .GroupBy(s => s.StatName)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Value!.Value));

        var propLines = await db.PlayerPropLines
            .Include(l => l.Game)
            .Where(l => l.PlayerId == id && l.Game.GameDate >= today)
            .OrderBy(l => l.Game.GameDate)
            .ToListAsync(ct);

        var props = new List<PropEstimate>();
        foreach (var line in propLines)
        {
            props.Add(await BuildPropEstimateAsync(player, line, statsStart, db, ct));
        }

        var injuries = await InjuryLookup.GetForPlayersAsync(db, [player.Id], ct);
        injuries.TryGetValue(player.Id, out var injury);

        return Results.Ok(new PlayerCardResponse(
            player.Id,
            player.Name,
            player.Team?.Abbreviation,
            seasonRows.Select(s => s.GameId).Distinct().Count(),
            seasonStats,
            props,
            injury.Status,
            injury.Note
        ));
    }

    // Reusable per-market league context: what every team allows in a stat category,
    // computed once and shared across every prop line that asks about the same market —
    // see BuildPropEstimateAsync's leagueCache parameter for why this exists.
    internal record LeagueAllowedContext(Dictionary<int, List<decimal>> AllowedByTeam, decimal? LeagueAllowedAverage);

    // Internal, not private — GameEndpoints reuses this to list every prop for a game
    // (the "which players have props tonight" view), not just one player's own card.
    //
    // teamsById/leagueCache are optional, request-scoped caches a caller can share across
    // many calls in a loop (HotBetsEndpoints does, across a whole week's props) — without
    // them every call independently re-queries the team table and re-runs a league-wide
    // aggregate query, which is fine for a single game/player but scales badly across a
    // whole slate: many prop lines share the same market ("Receiving Yards" appears on
    // most games), so the league-wide side of the computation is identical for all of
    // them and only needs to run once per market, not once per prop line.
    internal static async Task<PropEstimate> BuildPropEstimateAsync(
        Player player,
        PlayerPropLine line,
        DateOnly statsStart,
        NbaTrackerDbContext db,
        CancellationToken ct,
        Dictionary<int, Team>? teamsById = null,
        Dictionary<string, LeagueAllowedContext>? leagueCache = null)
    {
        int? opponentTeamId = player.TeamId switch
        {
            var t when t == line.Game.HomeTeamId => line.Game.AwayTeamId,
            var t when t == line.Game.AwayTeamId => line.Game.HomeTeamId,
            _ => null   // player's last-known team doesn't match either side (stale/traded, or
                        // discovered via prop-name search before their first box score) — unknown
        };

        Team? opponentTeam = null;
        if (opponentTeamId.HasValue)
        {
            if (teamsById is not null)
                teamsById.TryGetValue(opponentTeamId.Value, out opponentTeam);
            else
                opponentTeam = await db.Teams.FirstOrDefaultAsync(t => t.Id == opponentTeamId, ct);
        }

        var mapping = PropMarketMapping.TryGetMapping(line.MarketKey);
        var label = mapping?.Label ?? PropMarketMapping.PrettifyKey(line.MarketKey);
        var category = mapping?.DisplayCategory ?? "Other";

        // No stat mapping for this market yet — show it in the list with a readable label,
        // but there's no way to compute a history against it.
        if (mapping is null)
        {
            return new PropEstimate(
                line.Id, line.MarketKey, label, category, line.Line, line.OverOdds, line.UnderOdds,
                line.Bookmaker, line.UnderBookmaker, opponentTeam?.Abbreviation ?? "?", "current",
                0, null, 0, null, null, null, null);
        }

        var keyword = mapping.Keyword.ToLower();
        var exclude = mapping.Exclude;
        var today = ApiClock.Today;
        var priorSeason = SeasonHelper.PriorCompletedSeason(player.Sport, today);

        // Category+keyword rows, fuzzy-matched (see PropMarketMapping for why this isn't an
        // exact StatName match), scoped to either this season (by preseason-cutoff date) or
        // the prior completed season (by Season string).
        IQueryable<PlayerGameStat> Scoped(bool prior)
        {
            var q = db.PlayerGameStats.Where(s =>
                mapping.StatCategories.Contains(s.Category)
                && s.StatName.ToLower().Contains(keyword)
                && s.Value.HasValue);
            return prior
                ? q.Where(s => s.Game.Season == priorSeason)
                : q.Where(s => s.Game.GameDate >= statsStart);
        }

        // Player's own per-game values, disambiguated per game by taking the largest
        // matching value (a real total like "yards" is reliably larger than a per-attempt
        // average or a single-play long).
        var currentPerGame = await PerGameValuesAsync(Scoped(false).Where(s => s.PlayerId == player.Id), exclude, ct);

        var playerPerGame = currentPerGame;
        var statBasis = "current";
        var usePrior = currentPerGame.Count < MinGamesForEstimate && priorSeason is not null;
        if (usePrior)
        {
            var priorPerGame = await PerGameValuesAsync(Scoped(true).Where(s => s.PlayerId == player.Id), exclude, ct);
            if (priorPerGame.Count > currentPerGame.Count)
            {
                playerPerGame = priorPerGame;
                statBasis = "prior";
            }
        }

        int gamesWithData = playerPerGame.Count;
        decimal? seasonAverage = gamesWithData > 0 ? playerPerGame.Average() : null;
        int hitCount = playerPerGame.Count(v => v > line.Line);
        decimal? hitRatePct = gamesWithData > 0
            ? Math.Round(100m * hitCount / gamesWithData, 1)
            : null;

        // Without a resolved opponent there's no defensive context to compute — return the
        // player's own history (which may be the prior season) and stop there.
        if (opponentTeam is null)
        {
            return new PropEstimate(
                line.Id, line.MarketKey, label, category, line.Line, line.OverOdds, line.UnderOdds,
                line.Bookmaker, line.UnderBookmaker, "?", statBasis,
                gamesWithData, seasonAverage, hitCount, hitRatePct, null, null, null);
        }

        // League-wide context for this market+season-basis — what every team allows,
        // relative to average. Shared across every prop line asking about the same
        // market when a leagueCache is provided, since this side of the computation
        // doesn't depend on the specific player or line, only the market.
        var cacheKey = $"{string.Join(',', mapping.StatCategories)}|{keyword}|{statBasis}";
        LeagueAllowedContext leagueCtx;
        if (leagueCache is not null && leagueCache.TryGetValue(cacheKey, out var cachedCtx))
        {
            leagueCtx = cachedCtx;
        }
        else
        {
            leagueCtx = await GetLeagueAllowedContextAsync(db, () => Scoped(statBasis == "prior"), exclude, ct);
            if (leagueCache is not null) leagueCache[cacheKey] = leagueCtx;
        }

        decimal? leagueAllowedAverage = leagueCtx.LeagueAllowedAverage;
        var opponentAllowedGames = leagueCtx.AllowedByTeam.TryGetValue(opponentTeamId!.Value, out var oGames)
            ? oGames
            : [];
        decimal? opponentAllowedAverage = opponentAllowedGames.Count > 0
            ? opponentAllowedGames.Average()
            : null;

        decimal? estimatedHitRatePct = null;
        if (gamesWithData >= MinGamesForEstimate
            && opponentAllowedAverage.HasValue
            && leagueAllowedAverage is > 0)
        {
            var opponentFactor = opponentAllowedAverage.Value / leagueAllowedAverage.Value;
            var adjustedLine = opponentFactor > 0 ? line.Line / opponentFactor : line.Line;
            int adjustedHitCount = playerPerGame.Count(v => v > adjustedLine);
            estimatedHitRatePct = Math.Round(100m * adjustedHitCount / gamesWithData, 1);
        }

        return new PropEstimate(
            line.Id, line.MarketKey, label, category, line.Line, line.OverOdds, line.UnderOdds,
            line.Bookmaker, line.UnderBookmaker, opponentTeam.Abbreviation, statBasis,
            gamesWithData, seasonAverage, hitCount, hitRatePct,
            opponentAllowedAverage, leagueAllowedAverage, estimatedHitRatePct);
    }

    // Runs the league-wide "what does every team allow in this stat" query once. Extracted
    // out of BuildPropEstimateAsync so its result can be cached and shared across many prop
    // lines for the same market (see BuildPropEstimateAsync's leagueCache parameter).
    private static async Task<LeagueAllowedContext> GetLeagueAllowedContextAsync(
        NbaTrackerDbContext db, Func<IQueryable<PlayerGameStat>> scopedQuery, string[] exclude, CancellationToken ct)
    {
        var leagueRowsRaw = await scopedQuery()
            .Select(s => new { s.PlayerId, s.GameId, s.TeamId, s.StatName, s.Value, s.Game.HomeTeamId, s.Game.AwayTeamId })
            .ToListAsync(ct);

        var leagueRows = leagueRowsRaw
            .Where(r => !exclude.Any(x => r.StatName.ToLower().Contains(x)))
            .ToList();

        // Disambiguate per player per game first, then sum across players in that game
        // to get "total allowed by the opposing defense," grouped by (defenseTeamId, gameId)
        var perPlayerGame = leagueRows
            .GroupBy(r => new { r.PlayerId, r.GameId })
            .Select(g =>
            {
                var first = g.First();
                var defenseTeamId = first.HomeTeamId == first.TeamId ? first.AwayTeamId : first.HomeTeamId;
                return new { defenseTeamId, first.GameId, Value = g.Max(x => x.Value!.Value) };
            })
            .ToList();

        var allowedPerTeamGame = perPlayerGame
            .GroupBy(x => new { x.defenseTeamId, x.GameId })
            .Select(g => new { g.Key.defenseTeamId, Allowed = g.Sum(x => x.Value) })
            .ToList();

        var allowedByTeam = allowedPerTeamGame
            .GroupBy(x => x.defenseTeamId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Allowed).ToList());

        decimal? leagueAllowedAverage = allowedPerTeamGame.Count > 0
            ? allowedPerTeamGame.Average(x => x.Allowed)
            : null;

        return new LeagueAllowedContext(allowedByTeam, leagueAllowedAverage);
    }

    // Per-game values for a stat query: one number per game, taking the largest matching
    // entry when a game has several (see BuildPropEstimateAsync for why largest wins).
    // `exclude` drops sub-stat noise ("Yards Per Reception", "Longest Rush", …) that shares
    // a keyword with the counting stat but would win the largest-value tie-break.
    private static async Task<List<decimal>> PerGameValuesAsync(
        IQueryable<PlayerGameStat> query, string[] exclude, CancellationToken ct)
    {
        var rows = await query
            .Select(s => new { s.GameId, s.StatName, s.Value })
            .ToListAsync(ct);
        return rows
            .Where(r => !exclude.Any(x => r.StatName.ToLower().Contains(x)))
            .GroupBy(r => r.GameId)
            .Select(g => g.Max(r => r.Value!.Value))
            .ToList();
    }

    // GET /api/{sport}/leaderboards/{category} — season stat totals per player in one category.
    // No hardcoded "primary" stat to sort by: the exact stat-name vocabulary Highlightly
    // returns isn't fully known yet, so every captured stat comes back and the client
    // sorts by whichever column it wants (same pattern as the team grid).
    private static async Task<IResult> GetLeaderboardAsync(
        string sport,
        string category,
        NbaTrackerDbContext db,
        CancellationToken ct,
        string? season = null)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        if (!Categories.Contains(category))
            return Results.NotFound(new { error = $"Unknown category: {category}" });

        var today = ApiClock.Today;
        var (resolvedSeason, statsStart) = SeasonHelper.ResolveSeasonWindow(sport, season, today);

        var rows = await db.PlayerGameStats
            .Include(s => s.Player).ThenInclude(p => p.Team)
            .Include(s => s.Game)
            .Where(s => s.Player.Sport == sport
                     && s.Category.ToLower() == category.ToLower()
                     && s.Game.Season == resolvedSeason
                     && s.Game.GameDate >= statsStart)
            .ToListAsync(ct);

        var leaderboard = rows
            .GroupBy(s => s.PlayerId)
            .Select(g =>
            {
                var player = g.First().Player;
                var statTotals = g
                    .Where(s => s.Value.HasValue)
                    .GroupBy(s => s.StatName)
                    .ToDictionary(sg => sg.Key, sg => sg.Sum(s => s.Value!.Value));

                return new LeaderboardEntryResponse(
                    player.Id,
                    player.Name,
                    player.Team?.Abbreviation,
                    g.Select(s => s.GameId).Distinct().Count(),
                    statTotals
                );
            })
            .OrderBy(e => e.Name)
            .ToList();

        return Results.Ok(leaderboard);
    }
}
