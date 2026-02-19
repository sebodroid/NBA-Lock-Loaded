using System.Runtime.InteropServices;
using NbaTracker.Worker.Services;

namespace NbaTracker.Worker.Tests;

public class GameMatchingServiceTests
{
    private static TimeZoneInfo GetEasternTime() =>
        TimeZoneInfo.FindSystemTimeZoneById(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Eastern Standard Time"
                : "America/New_York");

    // -----------------------------------------------------------------------
    // BuildCanonicalKeyFromBdl
    // -----------------------------------------------------------------------

    [Fact]
    public void BuildCanonicalKeyFromBdl_ProducesExpectedKey()
    {
        var key = GameMatchingService.BuildCanonicalKeyFromBdl(
            season: 2025,
            homeAbbr: "BOS",
            awayAbbr: "LAL",
            gameDate: new DateOnly(2025, 11, 1));

        Assert.Equal("2025-26_BOS_LAL_2025-11-01", key);
    }

    // -----------------------------------------------------------------------
    // BuildCanonicalKeyFromOddsApi — ET conversion
    // -----------------------------------------------------------------------

    [Fact]
    public void BuildCanonicalKeyFromOddsApi_CommenceTimeNextUtcDay_UsesEtDate()
    {
        // 2025-11-01T00:30:00Z = Oct 31 in ET (Eastern is UTC-4 in EDT or UTC-5 in EST)
        // In November, clocks fall back on Nov 2 2025, so Nov 1 at 00:30 UTC = Oct 31 at 20:30 EDT (still EDT)
        // Eastern at that moment is still EDT (UTC-4), so 00:30 UTC = Oct 31 20:30 ET
        var commenceTime = new DateTimeOffset(2025, 11, 1, 0, 30, 0, TimeSpan.Zero);
        var easternTime = GetEasternTime();

        var key = GameMatchingService.BuildCanonicalKeyFromOddsApi(
            season: 2025,
            oddsApiHomeTeam: "Boston Celtics",
            oddsApiAwayTeam: "Los Angeles Lakers",
            commenceTimeUtc: commenceTime,
            easternTime: easternTime);

        Assert.Equal("2025-26_BOS_LAL_2025-10-31", key);
    }

    [Fact]
    public void BuildCanonicalKeyFromOddsApi_CommenceTimeSameDay_UsesCorrectDate()
    {
        // 2025-11-01T20:00:00Z is still Nov 1 in ET (UTC-4, so 16:00 ET)
        var commenceTime = new DateTimeOffset(2025, 11, 1, 20, 0, 0, TimeSpan.Zero);
        var easternTime = GetEasternTime();

        var key = GameMatchingService.BuildCanonicalKeyFromOddsApi(
            season: 2025,
            oddsApiHomeTeam: "Boston Celtics",
            oddsApiAwayTeam: "Los Angeles Lakers",
            commenceTimeUtc: commenceTime,
            easternTime: easternTime);

        Assert.Equal("2025-26_BOS_LAL_2025-11-01", key);
    }

    // -----------------------------------------------------------------------
    // TeamNameToAbbreviation — all 30 NBA teams
    // -----------------------------------------------------------------------

    [Fact]
    public void TeamNameToAbbreviation_ContainsAll30NbaTeams()
    {
        var dict = GameMatchingService.TeamNameToAbbreviation;

        Assert.Equal(30, dict.Count);

        // Spot-check every team to ensure no KeyNotFoundException on any entry
        var expectedTeams = new[]
        {
            "Atlanta Hawks", "Boston Celtics", "Brooklyn Nets", "Charlotte Hornets",
            "Chicago Bulls", "Cleveland Cavaliers", "Dallas Mavericks", "Denver Nuggets",
            "Detroit Pistons", "Golden State Warriors", "Houston Rockets", "Indiana Pacers",
            "Los Angeles Clippers", "Los Angeles Lakers", "Memphis Grizzlies", "Miami Heat",
            "Milwaukee Bucks", "Minnesota Timberwolves", "New Orleans Pelicans", "New York Knicks",
            "Oklahoma City Thunder", "Orlando Magic", "Philadelphia 76ers", "Phoenix Suns",
            "Portland Trail Blazers", "Sacramento Kings", "San Antonio Spurs", "Toronto Raptors",
            "Utah Jazz", "Washington Wizards"
        };

        foreach (var teamName in expectedTeams)
        {
            Assert.True(dict.ContainsKey(teamName), $"Missing team: {teamName}");
            Assert.False(string.IsNullOrWhiteSpace(dict[teamName]), $"Empty abbreviation for: {teamName}");
        }
    }
}
