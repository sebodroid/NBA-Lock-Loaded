using System.Runtime.InteropServices;

namespace NbaTracker.Worker.Services;

/// <summary>
/// Builds canonical keys for cross-API game matching (BallDontLie vs The Odds API).
/// Key format: {season}-{(season+1)%100:D2}_{homeAbbr}_{awayAbbr}_{date:yyyy-MM-dd}
/// Example: "2025-26_BOS_LAL_2025-11-01"
/// </summary>
public static class GameMatchingService
{
    /// <summary>
    /// Builds a canonical key from BallDontLie game data.
    /// The game date is already in local/ET terms from BDL.
    /// </summary>
    public static string BuildCanonicalKeyFromBdl(
        int season, string homeAbbr, string awayAbbr, DateOnly gameDate)
        => $"{season}-{(season + 1) % 100:D2}_{homeAbbr}_{awayAbbr}_{gameDate:yyyy-MM-dd}";

    /// <summary>
    /// Builds a canonical key from Odds API event data.
    /// Converts commenceTimeUtc to Eastern Time before taking the date,
    /// matching BDL's date convention (e.g. a game at 00:30 UTC on Nov 1 is Oct 31 in ET).
    /// </summary>
    public static string BuildCanonicalKeyFromOddsApi(
        int season,
        string oddsApiHomeTeam,
        string oddsApiAwayTeam,
        DateTimeOffset commenceTimeUtc,
        TimeZoneInfo easternTime)
    {
        var homeAbbr = TeamNameToAbbreviation[oddsApiHomeTeam];
        var awayAbbr = TeamNameToAbbreviation[oddsApiAwayTeam];
        var etTime = TimeZoneInfo.ConvertTime(commenceTimeUtc, easternTime);
        var gameDate = DateOnly.FromDateTime(etTime.DateTime);
        return BuildCanonicalKeyFromBdl(season, homeAbbr, awayAbbr, gameDate);
    }

    /// <summary>
    /// Gets the Eastern Time zone — uses the correct ID for both Windows and Linux.
    /// </summary>
    public static TimeZoneInfo GetEasternTimeZone() =>
        TimeZoneInfo.FindSystemTimeZoneById(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Eastern Standard Time"
                : "America/New_York");

    /// <summary>
    /// Maps Odds API full team names to BallDontLie team abbreviations.
    /// All 30 NBA teams.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> TeamNameToAbbreviation =
        new Dictionary<string, string>
        {
            ["Atlanta Hawks"]          = "ATL",
            ["Boston Celtics"]         = "BOS",
            ["Brooklyn Nets"]          = "BKN",
            ["Charlotte Hornets"]      = "CHA",
            ["Chicago Bulls"]          = "CHI",
            ["Cleveland Cavaliers"]    = "CLE",
            ["Dallas Mavericks"]       = "DAL",
            ["Denver Nuggets"]         = "DEN",
            ["Detroit Pistons"]        = "DET",
            ["Golden State Warriors"]  = "GSW",
            ["Houston Rockets"]        = "HOU",
            ["Indiana Pacers"]         = "IND",
            ["Los Angeles Clippers"]   = "LAC",
            ["Los Angeles Lakers"]     = "LAL",
            ["Memphis Grizzlies"]      = "MEM",
            ["Miami Heat"]             = "MIA",
            ["Milwaukee Bucks"]        = "MIL",
            ["Minnesota Timberwolves"] = "MIN",
            ["New Orleans Pelicans"]   = "NOP",
            ["New York Knicks"]        = "NYK",
            ["Oklahoma City Thunder"]  = "OKC",
            ["Orlando Magic"]          = "ORL",
            ["Philadelphia 76ers"]     = "PHI",
            ["Phoenix Suns"]           = "PHX",
            ["Portland Trail Blazers"] = "POR",
            ["Sacramento Kings"]       = "SAC",
            ["San Antonio Spurs"]      = "SAS",
            ["Toronto Raptors"]        = "TOR",
            ["Utah Jazz"]              = "UTA",
            ["Washington Wizards"]     = "WAS"
        };
}
