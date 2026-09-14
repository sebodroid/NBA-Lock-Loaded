using Microsoft.EntityFrameworkCore;
using NbaTracker.Api.Models;
using NbaTracker.Data;
using NbaTracker.Data.Entities;

namespace NbaTracker.Api.Endpoints;

// Rule-based trend surfacing — no AI, no hardcoded stat names. Every insight carries
// its sample size explicitly, and a minimum sample gate keeps early-season noise
// (e.g. a single preseason game) from being presented as a real trend.
public static class InsightEndpoints
{
    private const int MinGamesForTeamTrend = 3;
    private const int AtsPctHot = 70;
    private const int AtsPctCold = 30;
    private const int MinStreakLength = 2;

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

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var statsStart = SeasonHelper.StatsStartDate(sport, today);

        var insights = new List<InsightResponse>();
        insights.AddRange(await BuildTeamInsightsAsync(sport, statsStart, db, ct));
        insights.AddRange(await BuildPlayerLeaderInsightsAsync(sport, statsStart, db, ct));

        return Results.Ok(insights);
    }

    private static async Task<List<InsightResponse>> BuildTeamInsightsAsync(
        string sport, DateOnly statsStart, NbaTrackerDbContext db, CancellationToken ct)
    {
        var teams = await db.Teams.Where(t => t.Sport == sport).ToListAsync(ct);
        var finalGames = await db.Games
            .Where(g => g.Sport == sport && g.Status == "FINAL" && g.GameDate >= statsStart)
            .Include(g => g.GameResult)
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
            int atsDecisions = atsCovers + atsLosses;

            if (atsDecisions > 0)
            {
                int atsPct = (int)Math.Round(100.0 * atsCovers / atsDecisions);
                if (atsPct >= AtsPctHot || atsPct <= AtsPctCold)
                {
                    insights.Add(new InsightResponse(
                        "team-ats",
                        $"{team.Name} is {atsCovers}-{atsLosses} against the spread ({atsPct}%) this season.",
                        gamesPlayed));
                }
            }

            var allGames = homeGames.Concat(awayGames).ToList();
            int overs  = allGames.Count(g => g.GameResult?.OuResult == OuResult.Over);
            int unders = allGames.Count(g => g.GameResult?.OuResult == OuResult.Under);
            int ouDecisions = overs + unders;

            if (ouDecisions > 0)
            {
                int ouPct = (int)Math.Round(100.0 * overs / ouDecisions);
                if (ouPct >= AtsPctHot || ouPct <= AtsPctCold)
                {
                    var direction = ouPct >= AtsPctHot ? "over" : "under";
                    insights.Add(new InsightResponse(
                        "team-ou",
                        $"{team.Name} games have gone {direction} in {(ouPct >= AtsPctHot ? overs : unders)} of {ouDecisions} this season.",
                        gamesPlayed));
                }
            }

            var streak = ComputeStreak(team.Id, allGames);
            if (Math.Abs(streak) >= MinStreakLength)
            {
                var word = streak > 0 ? "won" : "lost";
                insights.Add(new InsightResponse(
                    "team-streak",
                    $"{team.Name} has {word} {Math.Abs(streak)} straight.",
                    gamesPlayed));
            }
        }

        return insights;
    }

    private static int ComputeStreak(int teamId, List<Game> games)
    {
        var ordered = games.OrderByDescending(g => g.GameDate).ToList();
        if (ordered.Count == 0) return 0;

        bool Won(Game g) => (g.HomeTeamId == teamId && g.HomeScore > g.AwayScore)
                          || (g.AwayTeamId == teamId && g.AwayScore > g.HomeScore);

        bool firstWon = Won(ordered[0]);
        int streak = 0;
        foreach (var g in ordered)
        {
            if (Won(g) == firstWon) streak += firstWon ? 1 : -1;
            else break;
        }
        return streak;
    }

    private static async Task<List<InsightResponse>> BuildPlayerLeaderInsightsAsync(
        string sport, DateOnly statsStart, NbaTrackerDbContext db, CancellationToken ct)
    {
        var rows = await db.PlayerGameStats
            .Include(s => s.Player)
            .Include(s => s.Game)
            .Where(s => s.Player.Sport == sport && s.Game.GameDate >= statsStart && s.Value.HasValue)
            .ToListAsync(ct);

        var insights = new List<InsightResponse>();

        foreach (var categoryGroup in rows.GroupBy(s => s.Category))
        {
            // No hardcoded stat name: within a category, surface whichever single
            // (player, stat) total is largest — for counting stats this is reliably
            // the marquee number (yards) rather than something incidental.
            var leader = categoryGroup
                .GroupBy(s => new { s.PlayerId, s.StatName })
                .Select(g => new
                {
                    g.Key.StatName,
                    Player = g.First().Player,
                    Total = g.Sum(s => s.Value!.Value),
                    GamesPlayed = g.Select(s => s.GameId).Distinct().Count()
                })
                .OrderByDescending(x => x.Total)
                .FirstOrDefault();

            if (leader is null) continue;

            insights.Add(new InsightResponse(
                "player-leader",
                $"{leader.Player.Name} leads {categoryGroup.Key.ToLower()} with {leader.Total} {leader.StatName.ToLower()} this season.",
                leader.GamesPlayed));
        }

        return insights;
    }
}
