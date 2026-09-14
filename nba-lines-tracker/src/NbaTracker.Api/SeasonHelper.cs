using NbaTracker.Data;

namespace NbaTracker.Api;

// Season string format differs by sport (matches what each sync orchestrator writes):
// NBA spans two calendar years and is stored "2025-26"; NFL also spans two years
// (regular season starts in fall, playoffs run into Feb) but is stored as the
// starting year only ("2026"); MLB never crosses a year boundary.
public static class SeasonHelper
{
    public static string CurrentSeason(string sport, DateOnly today) => sport switch
    {
        "NBA" => BuildNbaSeasonString(today.Month >= 10 ? today.Year : today.Year - 1),
        "NFL" => (today.Month <= 2 ? today.Year - 1 : today.Year).ToString(),
        _     => today.Year.ToString()   // MLB
    };

    private static string BuildNbaSeasonString(int seasonYear)
        => $"{seasonYear}-{(seasonYear + 1) % 100:D2}";

    /// <summary>
    /// Earliest GameDate that should count toward "this season's" stats. Before the NFL
    /// regular season starts, preseason is all that exists, so nothing is excluded yet —
    /// once kickoff happens, preseason games silently drop out of every stats view without
    /// needing a manual flip. NBA/MLB have no preseason games in this data at all, so no cutoff.
    /// </summary>
    public static DateOnly StatsStartDate(string sport, DateOnly today) => sport switch
    {
        "NFL" => today >= SportSeasonBoundaries.NflRegularSeasonStart
            ? SportSeasonBoundaries.NflRegularSeasonStart
            : DateOnly.MinValue,
        _ => DateOnly.MinValue
    };

    /// <summary>
    /// Resolves which season string and preseason-exclusion date to filter by. With no
    /// requestedSeason, this is the live/current-season behavior above (auto-transitions
    /// as "today" crosses kickoff). With an explicit requestedSeason (e.g. "2025" for a
    /// historical backfill), it always returns that season's full window — no live
    /// transition logic, since a completed past season never needs one.
    /// </summary>
    public static (string Season, DateOnly StatsStart) ResolveSeasonWindow(
        string sport, string? requestedSeason, DateOnly today)
    {
        if (requestedSeason is not null)
        {
            var start = sport == "NFL"
                && int.TryParse(requestedSeason, out var year)
                && SportSeasonBoundaries.NflRegularSeasonStarts.TryGetValue(year, out var seasonStart)
                    ? seasonStart
                    : DateOnly.MinValue;
            return (requestedSeason, start);
        }

        return (CurrentSeason(sport, today), StatsStartDate(sport, today));
    }

    /// <summary>
    /// The most recent season that has fully finished (has a recorded end date), or null
    /// if there isn't one on record. Used as a fallback data source for prop estimates
    /// early in a new season, before a player has enough current-season games to judge.
    /// </summary>
    public static string? PriorCompletedSeason(string sport, DateOnly today)
    {
        if (sport != "NFL") return null;
        if (!int.TryParse(CurrentSeason(sport, today), out var current)) return null;
        var prior = current - 1;
        return SportSeasonBoundaries.NflSeasonEnds.ContainsKey(prior) ? prior.ToString() : null;
    }
}
