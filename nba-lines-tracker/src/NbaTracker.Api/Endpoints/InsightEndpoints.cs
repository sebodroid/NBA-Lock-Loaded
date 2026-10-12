using Microsoft.EntityFrameworkCore;
using NbaTracker.Api.Models;
using NbaTracker.Data;
using NbaTracker.Data.Entities;

namespace NbaTracker.Api.Endpoints;

// Rule-based trend surfacing — no AI, no hardcoded stat names. Every insight carries
// its sample size explicitly, and a minimum sample gate keeps early-season noise
// (e.g. a single preseason game) from being presented as a real trend.
//
// Deliberately scoped to angles nothing else in the app already covers: Hot Bets owns
// "which player prop should I bet on this upcoming game," so this stays team-level
// (ATS/O-U, including situational splits) and market-wide (which prop TYPES run hot or
// cold league-wide). A season win/loss streak or a counting-stat leaderboard doesn't
// tell a bettor anything actionable about a line, so neither is surfaced here.
public static class InsightEndpoints
{
    private const int MinGamesForTeamTrend = 3;
    private const int MinSplitDecisions = 3;
    private const int MinMarketTrendDecisions = 12;
    private const int HotPct = 70;
    private const int ColdPct = 30;

    // A situational split (home/road, favorite/underdog) only earns its own card when it
    // tells a DIFFERENT story than the season-wide number already shown — either the
    // direction flips, or it's at least this many points more extreme. Otherwise it's just
    // the same signal restated (a team that's 4-5 Under overall and 3-3 Under at home isn't
    // two trends, it's one trend with its home record folded in), which is clutter, not a
    // second insight. 15 was too low in practice — a situational split is a SUBSET of the
    // season games (the 3 home games are 3 of the season's 5), so it will often read as
    // "more extreme" by sample-size noise alone even when it isn't really a different team.
    // Confirmed via a real example: Bears 4-of-5 Under season (80%) vs. 3-of-3 Under at
    // home (100%) — a 20-point gap that still read as the same trend to a human, not two.
    private const int MinDivergencePts = 25;

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", GetInsightsAsync);
    }

    // GET /api/{sport}/insights
    private static async Task<IResult> GetInsightsAsync(
        string sport,
        NbaTrackerDbContext db,
        CancellationToken ct)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        var today = ApiClock.Today;
        var statsStart = SeasonHelper.StatsStartDate(sport, today);

        var insights = new List<InsightResponse>();
        insights.AddRange(await BuildTeamInsightsAsync(sport, statsStart, db, ct));
        insights.AddRange(await BuildMarketTrendInsightsAsync(sport, statsStart, db, ct));

        return Results.Ok(insights);
    }

    private static async Task<List<InsightResponse>> BuildTeamInsightsAsync(
        string sport, DateOnly statsStart, NbaTrackerDbContext db, CancellationToken ct)
    {
        var teams = await db.Teams.Where(t => t.Sport == sport).ToListAsync(ct);
        var finalGames = await db.Games
            .Where(g => g.Sport == sport && g.Status == "FINAL" && g.GameDate >= statsStart)
            .Include(g => g.GameResult)
            .Include(g => g.GameLine)
            .ToListAsync(ct);

        var insights = new List<InsightResponse>();

        foreach (var team in teams)
        {
            var homeGames = finalGames.Where(g => g.HomeTeamId == team.Id).ToList();
            var awayGames = finalGames.Where(g => g.AwayTeamId == team.Id).ToList();
            int gamesPlayed = homeGames.Count + awayGames.Count;
            if (gamesPlayed < MinGamesForTeamTrend) continue;

            int atsCovers = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Cover)
                          + awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Cover);
            int atsLosses = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Loss)
                          + awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Loss);

            var atsBlanket = AddPctInsight(insights, "team-ats", atsCovers, atsLosses, gamesPlayed,
                hot: $"{team.Name} is {atsCovers}-{atsLosses} against the spread this season.",
                cold: $"{team.Name} is {atsCovers}-{atsLosses} against the spread this season.");

            var allGames = homeGames.Concat(awayGames).ToList();
            int overs  = allGames.Count(g => g.GameResult?.OuResult == OuResult.Over);
            int unders = allGames.Count(g => g.GameResult?.OuResult == OuResult.Under);

            var (avgScored, avgAllowed) = AvgScoredAllowed(homeGames, awayGames);
            var ouBlanket = AddOuInsight(insights, team.Name, "this season", overs, unders, gamesPlayed, avgScored, avgAllowed);

            // Situational splits — a blanket season ATS/O-U number can hide the real signal
            // (a team that covers as a dog but gets crushed as a favorite isn't really a
            // "60% ATS" team, it's two very different teams depending on the number).
            var favoriteGames = allGames.Where(g => g.GameLine?.FavoriteTeamId == team.Id).ToList();
            var dogGames = allGames.Where(g => g.GameLine?.FavoriteTeamId is not null
                                             && g.GameLine.FavoriteTeamId != team.Id).ToList();

            int favCovers = CountAts(favoriteGames, team.Id, AtsResult.Cover);
            int favLosses = CountAts(favoriteGames, team.Id, AtsResult.Loss);
            AddPctInsight(insights, "team-ats", favCovers, favLosses, favoriteGames.Count,
                hot: $"{team.Name} is {favCovers}-{favLosses} ATS as the favorite this season.",
                cold: $"{team.Name} is {favCovers}-{favLosses} ATS as the favorite this season.",
                minDecisions: MinSplitDecisions, blanket: atsBlanket);

            int dogCovers = CountAts(dogGames, team.Id, AtsResult.Cover);
            int dogLosses = CountAts(dogGames, team.Id, AtsResult.Loss);
            AddPctInsight(insights, "team-ats", dogCovers, dogLosses, dogGames.Count,
                hot: $"{team.Name} is {dogCovers}-{dogLosses} ATS as the underdog this season.",
                cold: $"{team.Name} is {dogCovers}-{dogLosses} ATS as the underdog this season.",
                minDecisions: MinSplitDecisions, blanket: atsBlanket);

            int homeOvers  = homeGames.Count(g => g.GameResult?.OuResult == OuResult.Over);
            int homeUnders = homeGames.Count(g => g.GameResult?.OuResult == OuResult.Under);
            var (homeAvgScored, homeAvgAllowed) = AvgScoredAllowed(homeGames, []);
            AddOuInsight(insights, team.Name, "at home", homeOvers, homeUnders, homeGames.Count,
                homeAvgScored, homeAvgAllowed, MinSplitDecisions, blanket: ouBlanket);

            int awayOvers  = awayGames.Count(g => g.GameResult?.OuResult == OuResult.Over);
            int awayUnders = awayGames.Count(g => g.GameResult?.OuResult == OuResult.Under);
            var (awayAvgScored, awayAvgAllowed) = AvgScoredAllowed([], awayGames);
            AddOuInsight(insights, team.Name, "on the road", awayOvers, awayUnders, awayGames.Count,
                awayAvgScored, awayAvgAllowed, MinSplitDecisions, blanket: ouBlanket);
        }

        return insights;
    }

    // Points scored/allowed per game across a home+away split, from final scores
    // already on hand — no extra query, Game.HomeScore/AwayScore are set once a
    // game goes FINAL.
    private static (decimal AvgScored, decimal AvgAllowed) AvgScoredAllowed(
        List<Game> homeGames, List<Game> awayGames)
    {
        var scored = homeGames.Select(g => (decimal)(g.HomeScore ?? 0))
            .Concat(awayGames.Select(g => (decimal)(g.AwayScore ?? 0))).ToList();
        var allowed = homeGames.Select(g => (decimal)(g.AwayScore ?? 0))
            .Concat(awayGames.Select(g => (decimal)(g.HomeScore ?? 0))).ToList();

        return scored.Count > 0 ? (scored.Average(), allowed.Average()) : (0m, 0m);
    }

    private static int CountAts(List<Game> games, int teamId, AtsResult result) =>
        games.Count(g => (g.HomeTeamId == teamId && g.GameResult?.HomeAtsResult == result)
                       || (g.AwayTeamId == teamId && g.GameResult?.AwayAtsResult == result));

    // Returns the direction+pct it classified as (for the caller to pass back in as a
    // blanket baseline on a later situational call), or null if it didn't qualify as
    // hot/cold at all — which also means nothing to compare a situational split against,
    // so that split can never be suppressed as "redundant" with a trend that doesn't exist.
    private static (string Direction, int Pct)? AddPctInsight(
        List<InsightResponse> insights, string category, int hitCount, int missCount, int sampleSize,
        string hot, string cold, int minDecisions = MinGamesForTeamTrend,
        (string Direction, int Pct)? blanket = null)
    {
        int decisions = hitCount + missCount;
        if (decisions < minDecisions) return null;

        int pct = (int)Math.Round(100.0 * hitCount / decisions);
        string? direction = pct >= HotPct ? "cover" : pct <= ColdPct ? "loss" : null;
        if (direction is null) return null;

        if (IsRedundant(blanket, direction, pct)) return (direction, pct);

        insights.Add(new InsightResponse(category, direction == "cover" ? hot : cold, sampleSize));
        return (direction, pct);
    }

    private static (string Direction, int Pct)? AddOuInsight(
        List<InsightResponse> insights, string teamName, string scopeLabel,
        int overs, int unders, int sampleSize, decimal avgScored, decimal avgAllowed,
        int minDecisions = MinGamesForTeamTrend, (string Direction, int Pct)? blanket = null)
    {
        int decisions = overs + unders;
        if (decisions < minDecisions) return null;

        int overPct = (int)Math.Round(100.0 * overs / decisions);
        string? direction = overPct >= HotPct ? "over" : overPct <= ColdPct ? "under" : null;
        if (direction is null) return null;

        if (IsRedundant(blanket, direction, overPct)) return (direction, overPct);

        // Raw scoring context alongside the hit rate — "hits the over 80% of the time"
        // doesn't say whether that's a high-powered offense, a leaky defense, or both,
        // which matters for judging whether the trend is likely to keep holding up.
        var scoring = $"averaging {avgScored:0.1} scored, {avgAllowed:0.1} allowed";
        insights.Add(new InsightResponse("team-ou", direction == "over"
            ? $"{teamName} games have gone over in {overs} of {decisions} {scopeLabel} ({scoring})."
            : $"{teamName} games have gone under in {unders} of {decisions} {scopeLabel} ({scoring}).", sampleSize));
        return (direction, overPct);
    }

    private static bool IsRedundant((string Direction, int Pct)? blanket, string direction, int pct) =>
        blanket is not null
        && blanket.Value.Direction == direction
        && Math.Abs(blanket.Value.Pct - pct) < MinDivergencePts;

    // Market-wide signal: across every settled player prop this season, which market
    // TYPES (not individual players — that's Hot Bets' job) have been running hot or
    // cold against their own posted lines league-wide. A market sitting at 72% Under
    // all season is a real pricing signal, independent of which specific player you bet.
    private static async Task<List<InsightResponse>> BuildMarketTrendInsightsAsync(
        string sport, DateOnly statsStart, NbaTrackerDbContext db, CancellationToken ct)
    {
        var settledLines = await db.PlayerPropLines
            .Where(l => l.Player.Sport == sport && l.Game.Status == "FINAL" && l.Game.GameDate >= statsStart)
            .Select(l => new { l.PlayerId, l.GameId, l.MarketKey, l.Line })
            .ToListAsync(ct);

        var insights = new List<InsightResponse>();

        foreach (var marketGroup in settledLines.GroupBy(l => l.MarketKey).ToList())
        {
            var mapping = PropMarketMapping.TryGetMapping(marketGroup.Key);
            if (mapping is null) continue;

            var keyword = mapping.Keyword.ToLower();
            var exclude = mapping.Exclude;
            var gameIds = marketGroup.Select(l => l.GameId).Distinct().ToList();

            var statRowsRaw = await db.PlayerGameStats
                .Where(s => gameIds.Contains(s.GameId)
                         && mapping.StatCategories.Contains(s.Category)
                         && s.StatName.ToLower().Contains(keyword)
                         && s.Value.HasValue)
                .Select(s => new { s.PlayerId, s.GameId, s.StatName, s.Value })
                .ToListAsync(ct);

            var actualByPlayerGame = statRowsRaw
                .Where(r => !exclude.Any(x => r.StatName.ToLower().Contains(x)))
                .GroupBy(r => (r.PlayerId, r.GameId))
                .ToDictionary(g => g.Key, g => g.Max(r => r.Value!.Value));

            int overs = 0, unders = 0;
            foreach (var line in marketGroup)
            {
                // Game is FINAL, so a missing row is a confirmed zero, not pending data —
                // Highlightly omits zero-stat categories rather than writing an explicit 0.
                var actual = actualByPlayerGame.GetValueOrDefault((line.PlayerId, line.GameId), 0m);
                if (actual > line.Line) overs++;
                else if (actual < line.Line) unders++;
            }

            int decisions = overs + unders;
            if (decisions < MinMarketTrendDecisions) continue;

            int overPct = (int)Math.Round(100.0 * overs / decisions);
            if (overPct >= HotPct)
                insights.Add(new InsightResponse("market-trend",
                    $"{mapping.Label} props have gone Over in {overs} of {decisions} league-wide this season.", decisions));
            else if (overPct <= ColdPct)
                insights.Add(new InsightResponse("market-trend",
                    $"{mapping.Label} props have gone Under in {unders} of {decisions} league-wide this season.", decisions));
        }

        return insights;
    }
}
