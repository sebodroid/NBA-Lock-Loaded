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

    // z=1.96 ~ 95% confidence — the standard choice, not tuned for this app specifically.
    private const double WilsonZ = 1.96;

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", GetHotBetsAsync);
    }

    // Wilson score interval lower bound: a conservative, sample-size-aware read on "how
    // often does this actually hit" instead of the raw hits/n percentage. A 2-for-2 record
    // scores far lower here than an 8-for-8 one even though both show 100% raw — the fewer
    // games observed, the more the true rate could plausibly be lower than what's been seen
    // so far, and this discounts for that automatically rather than needing a hand-picked
    // games-played cutoff. https://en.wikipedia.org/wiki/Binomial_proportion_confidence_interval
    private static decimal WilsonLowerBoundPct(int hits, int n)
    {
        if (n <= 0) return 0m;
        double p = (double)hits / n;
        double z2 = WilsonZ * WilsonZ;
        double denominator = 1 + z2 / n;
        double center = p + z2 / (2 * n);
        double margin = WilsonZ * Math.Sqrt(p * (1 - p) / n + z2 / (4.0 * n * n));
        double lower = Math.Clamp((center - margin) / denominator, 0, 1);
        return (decimal)(lower * 100);
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

        var ranked = await GetRankedCandidatesAsync(sport, db, ct);
        return Results.Ok(ranked.Take(limit).ToList());
    }

    // Internal, not private — ParlayEndpoints reuses the exact same ranked/vetted pool
    // of player props (same hit-rate floor, same recent-form and opportunity vetoes) as
    // raw material for parlay legs, rather than re-deriving a separate, possibly
    // inconsistent notion of "a good prop bet."
    internal static async Task<List<HotBetEntry>> GetRankedCandidatesAsync(
        string sport, NbaTrackerDbContext db, CancellationToken ct)
    {
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
        var statsCache = new Dictionary<string, Dictionary<int, List<(DateOnly GameDate, decimal Value)>>>();

        var candidates = new List<HotBetEntry>();
        foreach (var line in lines)
        {
            injuries.TryGetValue(line.PlayerId, out var injury);
            if (IsOut(injury.Status)) continue;

            var estimate = await PlayerEndpoints.BuildPropEstimateAsync(
                line.Player, line, statsStart, db, ct, teamsById, leagueCache, statsCache);
            if (estimate.GamesWithData < PlayerEndpoints.MinGamesForEstimate) continue;

            // Confidence, not the raw point estimate, decides whether a prop qualifies and
            // where it ranks — a 2-for-2 record and a 6-for-8 record can show a similar or
            // even identical percentage, but they aren't equally trustworthy. The Wilson
            // lower bound discounts small samples automatically instead of needing a
            // hand-picked games-played cutoff, and it's what the "chance of hitting" number
            // implicitly promises: a number you can actually act on, not just "hasn't missed
            // yet in a tiny sample."
            var hits = estimate.EstimatedHitCount ?? estimate.HitCount;
            var confidence = WilsonLowerBoundPct(hits, estimate.GamesWithData);
            if (confidence < MinHitRatePct) continue;

            // Veto: looks good on paper, but has gone cold recently — don't recommend it.
            if (estimate.RecentGamesWithData >= MinRecentGamesForVeto
                && estimate.RecentHitRatePct is not null
                && estimate.RecentHitRatePct < RecentFormVetoPct) continue;

            // Veto: a high hit rate built on thin volume isn't a repeatable edge — see
            // PropMarketMapping's Opportunity* fields. Not every market tracks this
            // (OpportunityMinAvg is null for most), so this only fires where it applies.
            var opportunityFloor = PropMarketMapping.TryGetMapping(line.MarketKey)?.OpportunityMinAvg;
            if (opportunityFloor is not null
                && estimate.OpportunityAvg is not null
                && estimate.OpportunityAvg < opportunityFloor) continue;

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

        await SnapshotNewCandidatesAsync(sport, candidates, db, ct);

        return candidates
            .OrderByDescending(c => WilsonLowerBoundPct(c.Estimate.EstimatedHitCount ?? c.Estimate.HitCount, c.Estimate.GamesWithData))
            .ThenByDescending(c => c.Estimate.GamesWithData)
            .ToList();
    }

    // Records the FIRST time each (Game, Player, Market) ever qualified as a Hot Bet —
    // see PredictionSnapshot for why this matters. Idempotent: the unique index means a
    // candidate that already has a snapshot from an earlier request this week is simply
    // left alone, never overwritten with a later (possibly different) confidence number.
    // Every qualifying candidate gets snapshotted here, not just the top N a caller ends
    // up showing — the accuracy tracker should see the whole qualifying pool, not an
    // arbitrarily limited slice of it.
    private static async Task SnapshotNewCandidatesAsync(
        string sport, List<HotBetEntry> candidates, NbaTrackerDbContext db, CancellationToken ct)
    {
        if (candidates.Count == 0) return;

        var gameIds = candidates.Select(c => c.GameId).Distinct().ToList();
        var existingKeys = await db.PredictionSnapshots
            .Where(p => gameIds.Contains(p.GameId))
            .Select(p => new { p.GameId, p.PlayerId, p.MarketKey })
            .ToListAsync(ct);
        var existing = existingKeys.Select(k => (k.GameId, k.PlayerId, k.MarketKey)).ToHashSet();

        foreach (var c in candidates)
        {
            var key = (c.GameId, c.PlayerId, c.Estimate.MarketKey);
            if (existing.Contains(key)) continue;

            db.PredictionSnapshots.Add(new PredictionSnapshot
            {
                Sport = sport,
                GameId = c.GameId,
                PlayerId = c.PlayerId,
                MarketKey = c.Estimate.MarketKey,
                Line = c.Estimate.Line,
                ConfidencePct = c.Estimate.EstimatedHitRatePct ?? c.Estimate.HitRatePct ?? 0m,
                CreatedAt = DateTime.UtcNow
            });
            existing.Add(key);   // guards against duplicate candidates within this same batch
        }

        await db.SaveChangesAsync(ct);
    }
}
