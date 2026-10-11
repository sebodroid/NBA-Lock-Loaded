using System.Globalization;
using System.Text;
using NbaTracker.Worker.Models.OddsApi;

namespace NbaTracker.Worker.Services;

public static class CfbGameMatchingService
{
    private static readonly Dictionary<string, string> OddsNameAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Albany"]                       = "UAlbany Great Danes",
            ["East Texas A&M Lions"]         = "Texas A&M-Commerce Lions",
            ["Houston Baptist Huskies"]      = "Houston Christian Huskies",
            ["LIU Sharks"]                   = "Long Island University Sharks",
            ["Southeastern Louisiana Lions"] = "SE Louisiana Lions",
            ["St. Thomas (MN) Tommies"]      = "St. Thomas-Minnesota Tommies",
            ["UMass Minutemen"]              = "Massachusetts Minutemen",
            ["Youngstown St Penguins"]       = "Youngstown State Penguins",
        };

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

    public static bool IsSameTeam(string oddsName, string highlightlyName)
    {
        if (OddsNameAliases.TryGetValue(oddsName.Trim(), out var aliased))
            oddsName = aliased;

        var odds = Tokenize(oddsName);
        var highlightly = Tokenize(highlightlyName);
        if (odds.Length == 0 || highlightly.Length == 0) return false;

        return IsContainedIn(odds, highlightly) || IsContainedIn(highlightly, odds);
    }

    private static bool IsContainedIn(string[] subset, string[] superset) =>
        subset.All(s => superset.Any(t =>
            t == s || (s.Length >= 3 && t.StartsWith(s, StringComparison.Ordinal))));

    public static DateOnly EasternDate(DateTimeOffset commenceTime, TimeZoneInfo easternTime)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(commenceTime, easternTime).DateTime);

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
