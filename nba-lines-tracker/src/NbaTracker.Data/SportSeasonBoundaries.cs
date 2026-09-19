namespace NbaTracker.Data;

// Single source of truth for season-start dates that need updating every year.
// Referenced by both the Worker (backfill bounds, Odds API sport-key selection)
// and the Api (excluding preseason games from season-stats aggregation).
public static class SportSeasonBoundaries
{
    // 2026 NFL regular season opener — Wednesday, Sept 9 (moved off the usual
    // Thursday because of the Australia game on the 10th).
    public static readonly DateOnly NflRegularSeasonStart = new(2026, 9, 9);

    // Regular-season start date per season year — add a new entry each year, and
    // whenever a season is added here also add its end date to NflSeasonEnds below.
    // Used for historical backfills and for season-aware Odds API sport-key selection
    // (NflRegularSeasonStart above is a convenience alias for the current year only).
    public static readonly IReadOnlyDictionary<int, DateOnly> NflRegularSeasonStarts = new Dictionary<int, DateOnly>
    {
        [2025] = new DateOnly(2025, 9, 4),
        [2026] = NflRegularSeasonStart,
    };

    // Last real game of that season (Super Bowl date) — bounds a historical backfill
    // so it doesn't overreach into the next season's preseason.
    public static readonly IReadOnlyDictionary<int, DateOnly> NflSeasonEnds = new Dictionary<int, DateOnly>
    {
        [2025] = new DateOnly(2026, 2, 8),   // Super Bowl LX
    };

    // College football (CFB). Season starts are the "Week 0" Saturday and ends are the CFP
    // national championship — both confirmed against Highlightly's NCAA match data. Bowl
    // and playoff games run into January but Highlightly tags them with the season's
    // starting year, matching how the NFL is stored. There is no CFB preseason, so unlike
    // the NFL these dates only bound backfills, not Odds API sport-key selection.
    public static readonly DateOnly CfbRegularSeasonStart = new(2026, 8, 29);

    public static readonly IReadOnlyDictionary<int, DateOnly> CfbRegularSeasonStarts = new Dictionary<int, DateOnly>
    {
        [2025] = new DateOnly(2025, 8, 23),
        [2026] = CfbRegularSeasonStart,
    };

    public static readonly IReadOnlyDictionary<int, DateOnly> CfbSeasonEnds = new Dictionary<int, DateOnly>
    {
        [2025] = new DateOnly(2026, 1, 19),   // CFP national championship
    };
}
