namespace NbaTracker.Api.Models;

public record AccuracyBucket(
    string Range,          // e.g. "65-75%"
    int Graded,             // snapshots in this bucket whose game has gone final
    int Hits,
    decimal? HitRatePct     // null if Graded == 0 — nothing to show yet, not a 0%
);

public record AccuracyResponse(
    int TotalSnapshots,
    int GradedSnapshots,
    int PendingSnapshots,   // game hasn't gone final yet
    int OverallHits,
    decimal? OverallHitRatePct,
    List<AccuracyBucket> Buckets
);
