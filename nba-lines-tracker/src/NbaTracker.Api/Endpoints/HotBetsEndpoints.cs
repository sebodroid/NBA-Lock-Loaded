using Microsoft.EntityFrameworkCore;
using NbaTracker.Api.Models;
using NbaTracker.Data;
using NbaTracker.Data.Entities;

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

    // A good season/opponent-adjusted number doesn't mean much if the player has gone
    // cold over their last few games — a role change, a nagging injury, or a defense
    // adjustment a season-long average can't see. Require at least this many recent
    // games before the recent-form check can veto a candidate at all (small samples are
    // too noisy to act on), then veto anything clearly diverging from its season signal.
    private const int MinRecentGamesForVeto = 3;
    private const decimal RecentFormVetoPct = 40m;

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

        var today = ApiClock.Today;
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

        var injuries = await InjuryLookup.GetForPlayersAsync(db, lines.Select(l => l.PlayerId), ct);

        // A player ruled Out (or on IR) shouldn't be "recommended" at all — Questionable/
        // Doubtful still show up, just tagged, since those players often do end up playing.
        static bool IsOut(string? status) => status is "Out" or "Injured Reserve";

        // Shared across every prop line below — this is the fix for Hot Bets being slow:
        // without these, BuildPropEstimateAsync independently re-queries the team table and
        // re-runs a league-wide aggregate query for every single prop line, even though a
        // whole week's slate has many lines sharing the same market (every game's
        // "Receiving Yards" prop asks the identical league-wide question). Computing each
        // market's league context once and reusing it here cuts what used to be dozens of
        // redundant full-league queries down to one per distinct market.
        var teamsById = await db.Teams.Where(t => t.Sport == sport).ToDictionaryAsync(t => t.Id, ct);
        var leagueCache = new Dictionary<string, PlayerEndpoints.LeagueAllowedContext>();

        var candidates = new List<HotBetEntry>();
        foreach (var line in lines)
        {
            injuries.TryGetValue(line.PlayerId, out var injury);
            if (IsOut(injury.Status)) continue;

            var estimate = await PlayerEndpoints.BuildPropEstimateAsync(
                line.Player, line, statsStart, db, ct, teamsById, leagueCache);
            if (estimate.GamesWithData < PlayerEndpoints.MinGamesForEstimate) continue;

            var pct = estimate.EstimatedHitRatePct ?? estimate.HitRatePct;
            if (pct is null || pct < MinHitRatePct) continue;

            // Veto: looks good on paper, but has gone cold recently — don't recommend it.
            if (estimate.RecentGamesWithData >= MinRecentGamesForVeto
                && estimate.RecentHitRatePct is not null
                && estimate.RecentHitRatePct < RecentFormVetoPct) continue;

            candidates.Add(new HotBetEntry(
                line.Player.Id,
                line.Player.Name,
                line.Player.Team?.Abbreviation,
                line.Game.Id,
                $"{line.Game.AwayTeam.Abbreviation} @ {line.Game.HomeTeam.Abbreviation}",
                line.Game.GameDate.ToString("yyyy-MM-dd"),
                estimate,
                injury.Status,
                injury.Note
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
