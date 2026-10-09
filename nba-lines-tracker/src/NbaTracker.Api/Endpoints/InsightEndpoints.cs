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

            AddPctInsight(insights, "team-ats", atsCovers, atsLosses, gamesPlayed,
                hot: $"{team.Name} is {atsCovers}-{atsLosses} against the spread this season.",
                cold: $"{team.Name} is {atsCovers}-{atsLosses} against the spread this season.");

            var allGames = homeGames.Concat(awayGames).ToList();
            int overs  = allGames.Count(g => g.GameResult?.OuResult == OuResult.Over);
            int unders = allGames.Count(g => g.GameResult?.OuResult == OuResult.Under);

            AddOuInsight(insights, team.Name, "this season", overs, unders, gamesPlayed);

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
                minDecisions: MinSplitDecisions);

            int dogCovers = CountAts(dogGames, team.Id, AtsResult.Cover);
            int dogLosses = CountAts(dogGames, team.Id, AtsResult.Loss);
            AddPctInsight(insights, "team-ats", dogCovers, dogLosses, dogGames.Count,
                hot: $"{team.Name} is {dogCovers}-{dogLosses} ATS as the underdog this season.",
                cold: $"{team.Name} is {dogCovers}-{dogLosses} ATS as the underdog this season.",
                minDecisions: MinSplitDecisions);

            int homeOvers  = homeGames.Count(g => g.GameResult?.OuResult == OuResult.Over);
            int homeUnders = homeGames.Count(g => g.GameResult?.OuResult == OuResult.Under);
            AddOuInsight(insights, team.Name, "at home", homeOvers, homeUnders, homeGames.Count, MinSplitDecisions);

            int awayOvers  = awayGames.Count(g => g.GameResult?.OuResult == OuResult.Over);
            int awayUnders = awayGames.Count(g => g.GameResult?.OuResult == OuResult.Under);
            AddOuInsight(insights, team.Name, "on the road", awayOvers, awayUnders, awayGames.Count, MinSplitDecisions);
        }

        return insights;
    }

    private static int CountAts(List<Game> games, int teamId, AtsResult result) =>
        games.Count(g => (g.HomeTeamId == teamId && g.GameResult?.HomeAtsResult == result)
                       || (g.AwayTeamId == teamId && g.GameResult?.AwayAtsResult == result));

    private static void AddPctInsight(
        List<InsightResponse> insights, string category, int hitCount, int missCount, int sampleSize,
        string hot, string cold, int minDecisions = MinGamesForTeamTrend)
    {
        int decisions = hitCount + missCount;
        if (decisions < minDecisions) return;

        int pct = (int)Math.Round(100.0 * hitCount / decisions);
        if (pct >= HotPct) insights.Add(new InsightResponse(category, hot, sampleSize));
        else if (pct <= ColdPct) insights.Add(new InsightResponse(category, cold, sampleSize));
    }

    private static void AddOuInsight(
        List<InsightResponse> insights, string teamName, string scopeLabel,
        int overs, int unders, int sampleSize, int minDecisions = MinGamesForTeamTrend)
    {
        int decisions = overs + unders;
        if (decisions < minDecisions) return;

        int overPct = (int)Math.Round(100.0 * overs / decisions);
        if (overPct >= HotPct)
            insights.Add(new InsightResponse("team-ou",
                $"{teamName} games have gone over in {overs} of {decisions} {scopeLabel}.", sampleSize));
        else if (overPct <= ColdPct)
            insights.Add(new InsightResponse("team-ou",
                $"{teamName} games have gone under in {unders} of {decisions} {scopeLabel}.", sampleSize));
    }

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
