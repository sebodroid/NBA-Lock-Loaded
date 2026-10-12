using Microsoft.EntityFrameworkCore;
using NbaTracker.Api.Models;
using NbaTracker.Data;
using NbaTracker.Data.Entities;

namespace NbaTracker.Api.Endpoints;

// Combines two independently-vetted pools of legs — player props (Hot Bets' own ranked
// candidates, already past its hit-rate floor and recent-form/opportunity vetoes) and
// team total leans (new here) — into suggested parlays, ranked by OUR OWN combined
// confidence rather than the bookmaker's implied probability. Discovery only: this does
// not place or track parlay bets, which would need the Bet entity to support multiple
// legs — a bigger change than this pass covers.
public static class ParlayEndpoints
{
    private const int LookaheadDays = 7;

    // Stricter than the 2-game floor everywhere else (Player Card, Hot Bets). A thin
    // sample showing on a card with an honest game-count label is fine context; that same
    // thin sample becoming a parlay leg is different — stacking multiple shaky legs
    // compounds the risk in a way a standalone bet doesn't, and a binary per-game market
    // like Anytime TD is especially exposed to a "100% from 2 games" illusion.
    private const int MinGamesForParlay = 4;

    private const decimal HotPct = 70m;
    private const decimal ColdPct = 30m;

    // How many of the best individual legs (across both pools) to actually combine —
    // kept small deliberately: C(8,3) = 56 combinations is already plenty of parlays to
    // rank, and a leg that isn't even in the top 8 individually has no business being
    // parlayed in as a "best" pick.
    private const int LegPoolSize = 8;

    // Without this, the single highest-confidence leg (which barely drags down any
    // combination it's in) ends up anchoring EVERY returned suggestion — mathematically
    // "best," but useless as a set of actual options. Cap how many of the returned
    // suggestions any one leg can appear in so the list is genuinely varied.
    private const int MaxAppearancesPerLeg = 2;

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", GetParlaysAsync);
    }

    // GET /api/{sport}/parlays?limit=5&legSize=2 — suggested 2-leg (or 3-leg) parlays
    // built from the best currently-available props and team-total leans, ranked by our
    // own combined confidence.
    private static async Task<IResult> GetParlaysAsync(
        string sport, NbaTrackerDbContext db, CancellationToken ct, int limit = 5, int legSize = 2)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        limit = Math.Clamp(limit, 1, 20);
        legSize = Math.Clamp(legSize, 2, 3);

        var propCandidates = await HotBetsEndpoints.GetRankedCandidatesAsync(sport, db, ct);
        var propLegs = propCandidates
            .Where(c => c.Estimate.GamesWithData >= MinGamesForParlay)
            .Select(c => new ParlayLeg(
            "PlayerProp",
            $"{c.PlayerName} Over {c.Estimate.Line} {c.Estimate.MarketLabel}",
            c.GameId,
            c.GameLabel,
            c.Estimate.EstimatedHitRatePct ?? c.Estimate.HitRatePct ?? 0m,
            c.Estimate.OverOdds ?? -110,
            c.PlayerId,
            c.Estimate.PropLineId
        ));

        var totalLegs = await GetGameTotalLegsAsync(sport, db, ct);

        var pool = propLegs.Concat(totalLegs)
            .OrderByDescending(l => l.ConfidencePct)
            .Take(LegPoolSize)
            .ToList();

        var suggestions = new List<ParlaySuggestion>();
        foreach (var combo in Combinations(pool, legSize))
        {
            var legs = combo.ToList();
            decimal combinedProb = legs.Aggregate(1m, (acc, l) => acc * (l.ConfidencePct / 100m)) * 100m;
            decimal combinedDecimalOdds = legs.Aggregate(1m, (acc, l) => acc * AmericanToDecimal(l.Odds));

            suggestions.Add(new ParlaySuggestion(
                legs, Math.Round(combinedProb, 1), DecimalToAmerican(combinedDecimalOdds)));
        }

        var ranked = SelectDiverse(suggestions, limit);
        return Results.Ok(ranked);
    }

    // Greedy selection in probability order, skipping a candidate only when accepting it
    // would push one of its legs past MaxAppearancesPerLeg — not a full re-optimization,
    // just enough to stop one dominant leg from monopolizing every slot.
    private static List<ParlaySuggestion> SelectDiverse(List<ParlaySuggestion> candidates, int limit)
    {
        var selected = new List<ParlaySuggestion>();
        var appearances = new Dictionary<string, int>();

        foreach (var candidate in candidates.OrderByDescending(s => s.CombinedProbabilityPct))
        {
            if (selected.Count >= limit) break;

            var keys = candidate.Legs.Select(LegKey).ToList();
            if (keys.Any(k => appearances.GetValueOrDefault(k) >= MaxAppearancesPerLeg)) continue;

            selected.Add(candidate);
            foreach (var k in keys) appearances[k] = appearances.GetValueOrDefault(k) + 1;
        }

        return selected;
    }

    private static string LegKey(ParlayLeg leg) =>
        $"{leg.Kind}|{leg.GameId}|{leg.PlayerPropLineId}|{leg.Description}";

    // Per-upcoming-game total lean: the home team's own home O/U split and the away
    // team's own road O/U split (same situational-split logic as InsightEndpoints), but
    // applied to one specific upcoming matchup instead of surfaced as a season-wide
    // sentence. Only returns a leg when BOTH teams' situational trends agree on a
    // direction — disagreement (or either team without enough games) means no real signal
    // for this particular game, not a coin-flip worth suggesting.
    private static async Task<List<ParlayLeg>> GetGameTotalLegsAsync(
        string sport, NbaTrackerDbContext db, CancellationToken ct)
    {
        var today = ApiClock.Today;
        var endDate = today.AddDays(LookaheadDays - 1);
        var statsStart = SeasonHelper.StatsStartDate(sport, today);

        var upcomingGames = await db.Games
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Include(g => g.GameLine)
            .Where(g => g.Sport == sport
                     && g.GameDate >= today
                     && g.GameDate <= endDate
                     && g.Status != "FINAL"
                     && g.Status != "POSTPONED"
                     && g.GameLine != null && g.GameLine.Total != null)
            .ToListAsync(ct);

        if (upcomingGames.Count == 0) return [];

        var teamIds = upcomingGames.SelectMany(g => new[] { g.HomeTeamId, g.AwayTeamId }).Distinct().ToHashSet();
        var seasonGames = await db.Games
            .Include(g => g.GameResult)
            .Where(g => g.Sport == sport
                     && g.Status == "FINAL"
                     && g.GameDate >= statsStart
                     && (teamIds.Contains(g.HomeTeamId) || teamIds.Contains(g.AwayTeamId)))
            .ToListAsync(ct);

        var legs = new List<ParlayLeg>();
        foreach (var game in upcomingGames)
        {
            var homeHomeGames = seasonGames.Where(g => g.HomeTeamId == game.HomeTeamId).ToList();
            var awayRoadGames = seasonGames.Where(g => g.AwayTeamId == game.AwayTeamId).ToList();

            int homeOvers = homeHomeGames.Count(g => g.GameResult?.OuResult == OuResult.Over);
            int homeUnders = homeHomeGames.Count(g => g.GameResult?.OuResult == OuResult.Under);
            int awayOvers = awayRoadGames.Count(g => g.GameResult?.OuResult == OuResult.Over);
            int awayUnders = awayRoadGames.Count(g => g.GameResult?.OuResult == OuResult.Under);

            int homeDecisions = homeOvers + homeUnders;
            int awayDecisions = awayOvers + awayUnders;
            if (homeDecisions < MinGamesForParlay || awayDecisions < MinGamesForParlay) continue;

            decimal homeOverPct = 100m * homeOvers / homeDecisions;
            decimal awayOverPct = 100m * awayOvers / awayDecisions;

            string? direction = null;
            decimal confidence = 0m;
            if (homeOverPct >= HotPct && awayOverPct >= HotPct)
            {
                direction = "Over";
                confidence = (homeOverPct + awayOverPct) / 2;
            }
            else if (homeOverPct <= ColdPct && awayOverPct <= ColdPct)
            {
                direction = "Under";
                confidence = ((100m - homeOverPct) + (100m - awayOverPct)) / 2;
            }

            if (direction is null) continue;

            var odds = direction == "Over" ? game.GameLine!.OverOdds : game.GameLine!.UnderOdds;
            if (odds is null) continue;

            legs.Add(new ParlayLeg(
                "Total",
                $"{game.AwayTeam.Abbreviation} @ {game.HomeTeam.Abbreviation} {direction} {game.GameLine!.Total}",
                game.Id,
                $"{game.AwayTeam.Abbreviation} @ {game.HomeTeam.Abbreviation}",
                Math.Round(confidence, 1),
                odds.Value,
                null,
                null
            ));
        }

        return legs;
    }

    private static decimal AmericanToDecimal(int american) =>
        american > 0 ? 1m + american / 100m : 1m + 100m / -american;

    private static int DecimalToAmerican(decimal decimalOdds) =>
        decimalOdds >= 2m
            ? (int)Math.Round((decimalOdds - 1m) * 100m)
            : (int)Math.Round(-100m / (decimalOdds - 1m));

    // All k-element combinations of a list, order-independent (e.g. [A,B] and [B,A] count
    // once) — standard recursive combinations generator, fine at this scale (pool size 8).
    private static IEnumerable<IEnumerable<T>> Combinations<T>(List<T> items, int k)
    {
        if (k == 0) { yield return []; yield break; }
        for (int i = 0; i <= items.Count - k; i++)
        {
            foreach (var rest in Combinations(items.Skip(i + 1).ToList(), k - 1))
                yield return new[] { items[i] }.Concat(rest);
        }
    }
}
