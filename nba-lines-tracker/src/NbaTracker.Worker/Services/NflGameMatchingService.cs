namespace NbaTracker.Worker.Services;

/// <summary>
/// Maps The Odds API full team names for NFL to standard abbreviations.
/// UNVERIFIED: these abbreviations are the common ESPN-style scheme, not pulled from a
/// live Highlightly response. Highlightly's /teams "abbreviation" field must match these
/// values exactly for odds matching to work — spot-check after the first real sync
/// (mismatches fail safe: that team's odds are skipped and logged at Debug, not a crash).
/// </summary>
public static class NflGameMatchingService
{
    public static readonly Dictionary<string, string> TeamNameToAbbreviation =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Arizona Cardinals"]       = "ARI",
            ["Atlanta Falcons"]         = "ATL",
            ["Baltimore Ravens"]        = "BAL",
            ["Buffalo Bills"]           = "BUF",
            ["Carolina Panthers"]       = "CAR",
            ["Chicago Bears"]           = "CHI",
            ["Cincinnati Bengals"]      = "CIN",
            ["Cleveland Browns"]        = "CLE",
            ["Dallas Cowboys"]          = "DAL",
            ["Denver Broncos"]          = "DEN",
            ["Detroit Lions"]           = "DET",
            ["Green Bay Packers"]       = "GB",
            ["Houston Texans"]          = "HOU",
            ["Indianapolis Colts"]      = "IND",
            ["Jacksonville Jaguars"]    = "JAX",
            ["Kansas City Chiefs"]      = "KC",
            ["Las Vegas Raiders"]       = "LV",
            ["Los Angeles Chargers"]    = "LAC",
            ["Los Angeles Rams"]        = "LAR",
            ["Miami Dolphins"]          = "MIA",
            ["Minnesota Vikings"]       = "MIN",
            ["New England Patriots"]    = "NE",
            ["New Orleans Saints"]      = "NO",
            ["New York Giants"]         = "NYG",
            ["New York Jets"]           = "NYJ",
            ["Philadelphia Eagles"]     = "PHI",
            ["Pittsburgh Steelers"]     = "PIT",
            ["San Francisco 49ers"]     = "SF",
            ["Seattle Seahawks"]        = "SEA",
            ["Tampa Bay Buccaneers"]    = "TB",
            ["Tennessee Titans"]        = "TEN",
            ["Washington Commanders"]   = "WAS",
        };

    public static string BuildCanonicalKey(string homeAbbr, string awayAbbr, DateOnly date)
        => $"NFL:{date:yyyy-MM-dd}:{awayAbbr}@{homeAbbr}";

    public static string BuildCanonicalKeyFromOddsApi(
        string homeTeamName, string awayTeamName, DateTimeOffset commenceTime, TimeZoneInfo tz)
    {
        if (!TeamNameToAbbreviation.TryGetValue(homeTeamName, out var homeAbbr))
            throw new KeyNotFoundException($"Unknown NFL home team: '{homeTeamName}'");
        if (!TeamNameToAbbreviation.TryGetValue(awayTeamName, out var awayAbbr))
            throw new KeyNotFoundException($"Unknown NFL away team: '{awayTeamName}'");

        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(commenceTime, tz).DateTime);
        return BuildCanonicalKey(homeAbbr, awayAbbr, localDate);
    }
}
