namespace NbaTracker.Api;

// Maps an Odds API prop market key to the PlayerGameStat category(ies) it corresponds to,
// plus a keyword to match within StatName. This is deliberately a fuzzy keyword match,
// not an exact string match — Highlightly's exact stat-name vocabulary isn't fully
// confirmed yet (the one verified example was "Total Successful Passes," not something
// as clean as "Passing Yards"), so an exact-string dictionary risks silently matching
// nothing. Tighten these once real regular-season stat names are confirmed.
//
// Keyword choice matters for COUNT stats: BuildPropEstimateAsync/PropStatResolver break a
// multi-match tie by taking the largest value per game, which is right for yardage totals
// but wrong for counts (e.g. "Longest Reception" ~19 would beat "Receptions" ~4). So count
// markets use a keyword specific enough to hit only the counting stat — usually the plural
// form — or an Exclude list to filter out rate/longest-play noise.
public static class PropMarketMapping
{
    // StatCategories: every PlayerGameStat.Category to search across for this market —
    // almost always one, except "anytime TD" which can come from either rushing or
    // receiving. DisplayCategory: the single tab this market's props are grouped under in
    // the UI, which doesn't have to match StatCategories one-for-one.
    public record Mapping(string[] StatCategories, string DisplayCategory, string Keyword, string Label, string[] Exclude);

    // Rate / split / per-play stats that share a keyword with the counting stat we want.
    private static readonly string[] RateNoise =
        ["yard", "long", "percent", "average", "avg", "rating", "per "];

    private static readonly string[] None = [];

    private static Mapping Single(string category, string keyword, string label, string[] exclude) =>
        new([category], category, keyword, label, exclude);

    private static readonly Dictionary<string, Mapping> Map = new()
    {
        ["player_pass_yds"]          = Single("Passing",   "yard",       "Passing Yards",    None),
        ["player_pass_tds"]          = Single("Passing",   "touchdown",  "Passing TDs",       None),
        ["player_pass_completions"]  = Single("Passing",   "completion", "Completions",       RateNoise),
        ["player_rush_yds"]          = Single("Rushing",   "yard",       "Rushing Yards",     None),
        ["player_rush_attempts"]     = Single("Rushing",   "attempt",    "Rushing Attempts",  RateNoise),
        ["player_rush_longest"]      = Single("Rushing",   "long",       "Longest Rush",      None),
        ["player_reception_yds"]     = Single("Receiving", "yard",       "Receiving Yards",   None),
        ["player_receptions"]        = Single("Receiving", "reception",  "Receptions",        RateNoise),
        ["player_reception_longest"] = Single("Receiving", "long",       "Longest Reception", None),
        ["player_sacks"]             = Single("Defense",   "sack",       "Sacks",             None),
        ["player_solo_tackles"]      = Single("Defense",   "solo",       "Solo Tackles",      None),

        // Single-sided "does this player score a TD" market — no Under, no real line from
        // the sportsbook (see NflSyncOrchestrator, which stores it as Over 0.5). A TD can
        // come from either rushing or receiving, so this is the one market that spans two
        // stat categories instead of one.
        ["player_anytime_td"] = new(["Rushing", "Receiving"], "Touchdowns", "touchdown", "Anytime TD", None),
    };

    // Fixed display order for the category tabs — anything unmapped falls under "Other".
    public static readonly string[] CategoryOrder = ["Receiving", "Passing", "Rushing", "Touchdowns", "Defense", "Other"];

    public static Mapping? TryGetMapping(string marketKey) => Map.GetValueOrDefault(marketKey);

    public static string CategoryFor(string marketKey) => TryGetMapping(marketKey)?.DisplayCategory ?? "Other";

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
