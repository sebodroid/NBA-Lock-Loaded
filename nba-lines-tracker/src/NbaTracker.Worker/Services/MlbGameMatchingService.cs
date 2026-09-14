namespace NbaTracker.Worker.Services;

/// <summary>
/// Maps The Odds API full team names for MLB to standard abbreviations.
/// The Odds API uses city+nickname format — we map to the same abbreviations
/// the MLB Stats API returns so we can join across both data sources.
/// </summary>
public static class MlbGameMatchingService
{
    // Odds API name → MLB Stats API abbreviation
    public static readonly Dictionary<string, string> TeamNameToAbbreviation =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Arizona Diamondbacks"]      = "ARI",
            ["Atlanta Braves"]            = "ATL",
            ["Baltimore Orioles"]         = "BAL",
            ["Boston Red Sox"]            = "BOS",
            ["Chicago Cubs"]              = "CHC",
            ["Chicago White Sox"]         = "CWS",
            ["Cincinnati Reds"]           = "CIN",
            ["Cleveland Guardians"]       = "CLE",
            ["Colorado Rockies"]          = "COL",
            ["Detroit Tigers"]            = "DET",
            ["Houston Astros"]            = "HOU",
            ["Kansas City Royals"]        = "KC",
            ["Los Angeles Angels"]        = "LAA",
            ["Los Angeles Dodgers"]       = "LAD",
            ["Miami Marlins"]             = "MIA",
            ["Milwaukee Brewers"]         = "MIL",
            ["Minnesota Twins"]           = "MIN",
            ["New York Mets"]             = "NYM",
            ["New York Yankees"]          = "NYY",
            ["Oakland Athletics"]         = "OAK",
            ["Philadelphia Phillies"]     = "PHI",
            ["Pittsburgh Pirates"]        = "PIT",
            ["San Diego Padres"]          = "SD",
            ["San Francisco Giants"]      = "SF",
            ["Seattle Mariners"]          = "SEA",
            ["St. Louis Cardinals"]       = "STL",
            ["Tampa Bay Rays"]            = "TB",
            ["Texas Rangers"]             = "TEX",
            ["Toronto Blue Jays"]         = "TOR",
            ["Washington Nationals"]      = "WSH",
        };

    public static string BuildCanonicalKey(string homeAbbr, string awayAbbr, DateOnly date)
        => $"MLB:{date:yyyy-MM-dd}:{awayAbbr}@{homeAbbr}";

    public static string BuildCanonicalKeyFromOddsApi(
        string homeTeamName, string awayTeamName, DateTimeOffset commenceTime, TimeZoneInfo tz)
    {
        if (!TeamNameToAbbreviation.TryGetValue(homeTeamName, out var homeAbbr))
            throw new KeyNotFoundException($"Unknown MLB home team: '{homeTeamName}'");
        if (!TeamNameToAbbreviation.TryGetValue(awayTeamName, out var awayAbbr))
            throw new KeyNotFoundException($"Unknown MLB away team: '{awayTeamName}'");

        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(commenceTime, tz).DateTime);
        return BuildCanonicalKey(homeAbbr, awayAbbr, localDate);
    }
}