using Microsoft.EntityFrameworkCore;
using NbaTracker.Api.Models;
using NbaTracker.Data;
using NbaTracker.Data.Entities;

namespace NbaTracker.Api.Endpoints;

// The honest answer to "do the confidence numbers mean anything" — grades every
// PredictionSnapshot whose game has finaled against the real box score (never a stored
// status, same live-grading philosophy as BetEndpoints), then buckets by the confidence
// we originally gave it. If the 65-75% bucket isn't actually hitting somewhere around
// 65-75%, that's the thresholds lying, not bad luck, and it's exactly what this exists
// to catch.
public static class AccuracyEndpoints
{
    private static readonly (decimal Lo, decimal Hi, string Label)[] Buckets =
    [
        (65m, 75m, "65-75%"),
        (75m, 85m, "75-85%"),
        (85m, 95m, "85-95%"),
        (95m, 101m, "95-100%"),   // 101 so a 100% snapshot falls inside, not excluded at the edge
    ];

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", GetAccuracyAsync);
    }

    // GET /api/{sport}/accuracy
    private static async Task<IResult> GetAccuracyAsync(
        string sport, NbaTrackerDbContext db, CancellationToken ct)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        var snapshots = await db.PredictionSnapshots
            .Include(p => p.Game)
            .Where(p => p.Sport == sport)
            .ToListAsync(ct);

        int pending = 0, graded = 0, hits = 0;
        var bucketCounts = Buckets.ToDictionary(b => b.Label, _ => (Graded: 0, Hits: 0));

        foreach (var snap in snapshots)
        {
            if (snap.Game.Status != "FINAL") { pending++; continue; }

            var actual = await PropStatResolver.GetGameValueAsync(db, snap.PlayerId, snap.GameId, snap.MarketKey, ct);
            if (actual is null) { pending++; continue; }   // shouldn't happen for a FINAL game, but don't guess

            graded++;
            bool over = actual > snap.Line;
            bool hit = snap.Side == BetSide.Over ? over : !over;
            if (hit) hits++;

            var bucket = Buckets.FirstOrDefault(b => snap.ConfidencePct >= b.Lo && snap.ConfidencePct < b.Hi);
            if (bucket != default)
            {
                var (g, h) = bucketCounts[bucket.Label];
                bucketCounts[bucket.Label] = (g + 1, h + (hit ? 1 : 0));
            }
        }

        var bucketResponses = Buckets.Select(b =>
        {
            var (g, h) = bucketCounts[b.Label];
            return new AccuracyBucket(b.Label, g, h, g > 0 ? Math.Round(100m * h / g, 1) : null);
        }).ToList();

        return Results.Ok(new AccuracyResponse(
            snapshots.Count, graded, pending, hits,
            graded > 0 ? Math.Round(100m * hits / graded, 1) : null,
            bucketResponses
        ));
    }
}
