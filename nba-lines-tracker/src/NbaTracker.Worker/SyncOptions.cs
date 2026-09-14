namespace NbaTracker.Worker;

public class SyncOptions
{
    public bool IsBackfill { get; init; }
    public DateOnly? SyncDate { get; init; }

    // One-time historical backfill for a specific past NFL season (e.g. 2025), using
    // that season's own start/end dates from SportSeasonBoundaries. NFL-only — Worker.cs
    // and MlbWorker.cs don't read this, so NBA/MLB are unaffected by it.
    public int? NflHistoricalSeason { get; init; }
}
