using Microsoft.EntityFrameworkCore;
using NbaTracker.Api.Models;
using NbaTracker.Data;

namespace NbaTracker.Api.Endpoints;

public static class HotBetsEndpoints
{
    // How many days ahead of today to scan for candidate props — this week's slate,
    // not the whole season, since "hot bets" is meant as a short-term weekly pick list.
    private const int LookaheadDays = 7;

    // "Really high chance of hitting" per the user's own framing — a floor, not a target.
    // Fewer than `limit` qualifying props just means fewer results; the list is never
    // padded out with weaker bets to hit a round number.
    private const decimal MinHitRatePct = 65m;

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", GetHotBetsAsync);
    }

    // GET /api/{sport}/hot-bets?limit=10 — this week's best player-prop bets by estimated
    // hit rate, ranked across every upcoming game at once. Reuses the same per-prop
    // estimate PlayerEndpoints computes for a player card or a game's prop list — this
    // just scans all of them and surfaces the winners.
    private static async Task<IResult> GetHotBetsAsync(
        string sport, NbaTrackerDbContext db, CancellationToken ct, int limit = 10)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        limit = Math.Clamp(limit, 1, 25);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var endDate = today.AddDays(LookaheadDays - 1);
        var statsStart = SeasonHelper.StatsStartDate(sport, today);

        var lines = await db.PlayerPropLines
            .Include(l => l.Player).ThenInclude(p => p.Team)
            .Include(l => l.Game).ThenInclude(g => g.HomeTeam)
            .Include(l => l.Game).ThenInclude(g => g.AwayTeam)
            .Where(l => l.Player.Sport == sport
                     && l.Game.GameDate >= today
                     && l.Game.GameDate <= endDate
                     && l.Game.Status != "FINAL"
                     && l.Game.Status != "POSTPONED")
            .ToListAsync(ct);

        var candidates = new List<HotBetEntry>();
        foreach (var line in lines)
        {
            var estimate = await PlayerEndpoints.BuildPropEstimateAsync(line.Player, line, statsStart, db, ct);
            if (estimate.GamesWithData < PlayerEndpoints.MinGamesForEstimate) continue;

            var pct = estimate.EstimatedHitRatePct ?? estimate.HitRatePct;
            if (pct is null || pct < MinHitRatePct) continue;

            candidates.Add(new HotBetEntry(
                line.Player.Id,
                line.Player.Name,
                line.Player.Team?.Abbreviation,
                line.Game.Id,
                $"{line.Game.AwayTeam.Abbreviation} @ {line.Game.HomeTeam.Abbreviation}",
                line.Game.GameDate.ToString("yyyy-MM-dd"),
                estimate
            ));
        }

        var ranked = candidates
            .OrderByDescending(c => c.Estimate.EstimatedHitRatePct ?? c.Estimate.HitRatePct)
            .ThenByDescending(c => c.Estimate.GamesWithData)
            .Take(limit)
            .ToList();

        return Results.Ok(ranked);
    }
}
