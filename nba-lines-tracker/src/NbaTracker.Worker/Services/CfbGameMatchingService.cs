using System.Globalization;
using System.Text;
using NbaTracker.Worker.Models.OddsApi;

namespace NbaTracker.Worker.Services;

/// <summary>
/// Matches Highlightly NCAA games to Odds API events by team NAME, not abbreviation.
/// The NFL's fixed 32-team name-to-abbreviation table can't scale to college football:
/// Highlightly's NCAA feed has ~755 teams across every division and 150+ abbreviations
/// are shared by different schools (WIS = Wisconsin plus six others), so an abbreviation
/// key would be ambiguous. Names are compared as normalized token sets instead.
/// </summary>
public static class CfbGameMatchingService
{
    // Odds API name -> Highlightly displayName, for the cases token matching can't bridge:
    // renamed schools, and abbreviations ("St" is both State and Saint, so it can't be
    // expanded generically). Checked against ~270 real Odds API team names (FBS + FCS)
    // and Highlightly's NCAA team list — every other name resolves via TokensMatch.
    // Unmatched Odds events are logged by the orchestrator so new entries are easy to spot.
    private static readonly Dictionary<string, string> OddsNameAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Albany"]                       = "UAlbany Great Danes",   // token match would pick Albany State
            ["East Texas A&M Lions"]         = "Texas A&M-Commerce Lions",
            ["Houston Baptist Huskies"]      = "Houston Christian Huskies",
            ["LIU Sharks"]                   = "Long Island University Sharks",
            ["Southeastern Louisiana Lions"] = "SE Louisiana Lions",
            ["St. Thomas (MN) Tommies"]      = "St. Thomas-Minnesota Tommies",
            ["UMass Minutemen"]              = "Massachusetts Minutemen",
            ["Youngstown St Penguins"]       = "Youngstown State Penguins",
        };

    /// <summary>
    /// Lowercases, strips accents ("José" = "Jose"), drops apostrophes and periods
    /// ("Hawai'i" = "Hawaii"), turns "&amp;" into "and", and splits on everything else
    /// (so "Arkansas-Pine Bluff" and "Arkansas Pine Bluff" tokenize identically).
    /// </summary>
    public static string[] Tokenize(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (c is '\'' or '‘' or '’' or 'ʻ' or '.') continue;

            if (c == '&') sb.Append(" and ");
            else if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else sb.Append(' ');
        }
        return sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Whether an Odds API team name and a Highlightly team name are the same school.
    /// Each source drops or adds words the other doesn't ("Delaware Blue Hens" vs
    /// "Delaware Fightin' Blue Hens", "Sam Houston State Bearkats" vs "Sam Houston
    /// Bearkats"), so one name's tokens only need to be contained in the other's.
    /// </summary>
    public static bool IsSameTeam(string oddsName, string highlightlyName)
    {
        if (OddsNameAliases.TryGetValue(oddsName.Trim(), out var aliased))
            oddsName = aliased;

        var odds = Tokenize(oddsName);
        var highlightly = Tokenize(highlightlyName);
        if (odds.Length == 0 || highlightly.Length == 0) return false;

        return IsContainedIn(odds, highlightly) || IsContainedIn(highlightly, odds);
    }

    // A token matches equal tokens, or is a prefix (3+ chars) of a token in the other
    // name — only in the contained-in direction, which is what keeps "Southern Jaguars"
    // from matching "South Alabama Jaguars" and "Blue Devils" from matching "Bluefield".
    private static bool IsContainedIn(string[] subset, string[] superset) =>
        subset.All(s => superset.Any(t =>
            t == s || (s.Length >= 3 && t.StartsWith(s, StringComparison.Ordinal))));

    public static DateOnly EasternDate(DateTimeOffset commenceTime, TimeZoneInfo easternTime)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(commenceTime, easternTime).DateTime);

    /// <summary>
    /// Odds API events on the given Eastern date whose two teams are this game's two
    /// teams. Swapped is true when the Odds API lists the home team as the away team —
    /// common for neutral-site and bowl games, where books pick a "home" arbitrarily —
    /// so callers must map spread/odds by team, never by home/away position.
    /// More than one candidate means the match is ambiguous and should be skipped.
    /// </summary>
    public static List<(OddsApiEvent Event, bool Swapped)> FindCandidates(
        string homeName, string awayName, DateOnly date,
        IEnumerable<OddsApiEvent> events, TimeZoneInfo easternTime)
    {
        var candidates = new List<(OddsApiEvent, bool)>();
        foreach (var evt in events)
        {
            if (EasternDate(evt.CommenceTime, easternTime) != date) continue;

            if (IsSameTeam(evt.HomeTeam, homeName) && IsSameTeam(evt.AwayTeam, awayName))
                candidates.Add((evt, false));
            else if (IsSameTeam(evt.HomeTeam, awayName) && IsSameTeam(evt.AwayTeam, homeName))
                candidates.Add((evt, true));
        }
        return candidates;
    }
}
