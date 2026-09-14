namespace NbaTracker.Api;

// Maps an Odds API prop market key to the PlayerGameStat category it corresponds to,
// plus a keyword to match within StatName. This is deliberately a fuzzy keyword match,
// not an exact string match — Highlightly's exact stat-name vocabulary isn't fully
// confirmed yet (the one verified example was "Total Successful Passes," not something
// as clean as "Passing Yards"), so an exact-string dictionary risks silently matching
// nothing. Tighten these once real regular-season stat names are confirmed.
//
// The keyword alone isn't enough for COUNT markets: BuildPropEstimateAsync breaks a
// multi-match tie by taking the largest value per game, which is correct for yardage
// totals but wrong for counts — "Longest Reception" (~19) would beat "Receptions" (~4).
// So each mapping also carries an Exclude list of sub-stat noise ("Yards Per Reception",
// "Completion Percentage", "Longest Rush", …) that is filtered out before the tie-break.
public static class PropMarketMapping
{
    public record Mapping(string Category, string Keyword, string Label, string[] Exclude);

    // Rate / split / per-play stats that share a keyword with the counting stat we want.
    private static readonly string[] RateNoise =
        ["yard", "long", "percent", "average", "avg", "rating", "per "];

    private static readonly string[] None = [];

    private static readonly Dictionary<string, Mapping> Map = new()
    {
        ["player_pass_yds"]         = new("Passing",   "yard",       "Passing Yards",     None),
        ["player_pass_tds"]         = new("Passing",   "touchdown",  "Passing TDs",       None),
        ["player_pass_completions"] = new("Passing",   "completion", "Completions",       RateNoise),
        ["player_rush_yds"]         = new("Rushing",   "yard",       "Rushing Yards",     None),
        ["player_rush_attempts"]    = new("Rushing",   "attempt",    "Rushing Attempts",  RateNoise),
        ["player_reception_yds"]    = new("Receiving", "yard",       "Receiving Yards",   None),
        ["player_receptions"]       = new("Receiving", "reception",  "Receptions",        RateNoise),
        ["player_sacks"]            = new("Defense",   "sack",       "Sacks",             None),
        ["player_solo_tackles"]     = new("Defense",   "solo",       "Solo Tackles",      None),
    };

    // Fixed display order for the category tabs — anything unmapped falls under "Other".
    public static readonly string[] CategoryOrder = ["Receiving", "Passing", "Rushing", "Defense", "Other"];

    public static Mapping? TryGetMapping(string marketKey) => Map.GetValueOrDefault(marketKey);

    public static string CategoryFor(string marketKey) => TryGetMapping(marketKey)?.Category ?? "Other";

    // "player_rush_reception_yds" -> "Rush Reception Yds" — a readable fallback label
    // for a market we haven't explicitly mapped yet.
    public static string PrettifyKey(string marketKey)
    {
        var trimmed = marketKey.StartsWith("player_") ? marketKey["player_".Length..] : marketKey;
        var words = trimmed.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]);
        return string.Join(' ', words);
    }
}
