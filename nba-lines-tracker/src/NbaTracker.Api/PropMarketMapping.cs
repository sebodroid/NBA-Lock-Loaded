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
    //
    // Opportunity* fields are a second, independent signal from the stat itself: how often
    // the player is actually getting the ball, not just whether they cleared the line.
    // A high hit rate built on 3 targets/game isn't the repeatable kind of edge a high hit
    // rate on 9 targets/game is — the "Tez Johnson vs. Godwin" problem (one or the other can
    // have an outlier game regardless of role). Null means "not tracked for this market" —
    // passing props skip it (a healthy starting QB plays essentially every snap already,
    // so there's no real opportunity variance to catch the way there is for pass-catchers
    // and runners), and the Rush Attempts market itself skips it since attempts already IS
    // the opportunity measure, not something separate from it.
    // OpportunityComponents can hold more than one (category, keyword) pair — Anytime TD
    // needs both rush attempts AND targets summed, since either pathway can produce the
    // score. Each component's season average is computed independently (its own PerGame
    // query) and the averages are summed — a close-enough approximation of "average
    // combined opportunity" that avoids needing a harder per-game join, fine for a
    // heuristic signal rather than strict accounting.
    public record Mapping(
        string[] StatCategories, string DisplayCategory, string Keyword, string Label, string[] Exclude,
        (string Category, string Keyword)[]? OpportunityComponents = null, string? OpportunityLabel = null,
        decimal? OpportunityMinAvg = null);

    // Rate / split / per-play stats that share a keyword with the counting stat we want.
    private static readonly string[] RateNoise =
        ["yard", "long", "percent", "average", "avg", "rating", "per "];

    private static readonly string[] None = [];

    private static Mapping Single(string category, string keyword, string label, string[] exclude) =>
        new([category], category, keyword, label, exclude);

    private static Mapping WithOpportunity(
        string category, string keyword, string label, string[] exclude,
        string opportunityCategory, string opportunityKeyword, string opportunityLabel, decimal opportunityMinAvg) =>
        new([category], category, keyword, label, exclude,
            [(opportunityCategory, opportunityKeyword)], opportunityLabel, opportunityMinAvg);

    private static readonly Dictionary<string, Mapping> Map = new()
    {
        ["player_pass_yds"]          = Single("Passing",   "yard",       "Passing Yards",    None),
        ["player_pass_tds"]          = Single("Passing",   "touchdown",  "Passing TDs",       None),
        // Highlightly's real stat name is "Total Successful Passes," not "Completions" —
        // confirmed via a live stat-names query (2026-10-12). Keyword "completion" never
        // matched anything, so every Completions prop showed zero games/no estimate for
        // every player, silently, since this market has no Exclude list to flag the
        // all-zero result as suspicious.
        ["player_pass_completions"]  = Single("Passing",   "successful", "Completions",       RateNoise),
        ["player_rush_yds"]          = WithOpportunity("Rushing", "yard", "Rushing Yards", None,
                                            "Rushing", "attempt", "rush att/gm", 8m),
        ["player_rush_attempts"]     = Single("Rushing",   "attempt",    "Rushing Attempts",  RateNoise),
        ["player_rush_longest"]      = WithOpportunity("Rushing", "long", "Longest Rush", None,
                                            "Rushing", "attempt", "rush att/gm", 8m),
        ["player_reception_yds"]     = WithOpportunity("Receiving", "yard", "Receiving Yards", None,
                                            "Receiving", "target", "targets/gm", 4m),
        ["player_receptions"]        = WithOpportunity("Receiving", "reception", "Receptions", RateNoise,
                                            "Receiving", "target", "targets/gm", 4m),
        ["player_reception_longest"] = WithOpportunity("Receiving", "long", "Longest Reception", None,
                                            "Receiving", "target", "targets/gm", 4m),
        ["player_sacks"]             = Single("Defense",   "sack",       "Sacks",             None),
        ["player_solo_tackles"]      = Single("Defense",   "solo",       "Solo Tackles",      None),

        // Single-sided "does this player score a TD" market — no Under, no real line from
        // the sportsbook (see NflSyncOrchestrator, which stores it as Over 0.5). A TD can
        // come from either rushing or receiving, so this is the one market that spans two
        // stat categories instead of one. Opportunity = rush attempts + targets combined,
        // since either pathway can produce the score — this is the market most exposed to
        // the "100% from 2 games" small-sample trap (a binary per-game outcome, not a
        // yardage total), so it needs this check more than most, even though a single
        // combined-opportunity threshold is necessarily a rough fit across very different
        // player types (a mobile QB's and a bell-cow RB's baselines don't look alike).
        ["player_anytime_td"] = new(["Rushing", "Receiving"], "Touchdowns", "touchdown", "Anytime TD", None,
            [("Rushing", "attempt"), ("Receiving", "target")], "rush att + tgt/gm", 6m),
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
