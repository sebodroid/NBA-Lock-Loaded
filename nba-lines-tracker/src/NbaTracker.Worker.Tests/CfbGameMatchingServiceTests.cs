using System.Runtime.InteropServices;
using NbaTracker.Worker.Models.OddsApi;
using NbaTracker.Worker.Services;

namespace NbaTracker.Worker.Tests;

// Team names below are real strings from the Odds API and Highlightly's NCAA feed.
public class CfbGameMatchingServiceTests
{
    private static TimeZoneInfo GetEasternTime() =>
        TimeZoneInfo.FindSystemTimeZoneById(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Eastern Standard Time"
                : "America/New_York");

    private static OddsApiEvent Event(string id, string home, string away, DateTimeOffset commence) =>
        new() { Id = id, HomeTeam = home, AwayTeam = away, CommenceTime = commence };

    // -----------------------------------------------------------------------
    // Tokenize
    // -----------------------------------------------------------------------

    [Fact]
    public void Tokenize_StripsAccentsApostrophesAndPunctuation()
    {
        Assert.Equal(["san", "jose", "state", "spartans"], CfbGameMatchingService.Tokenize("San José State Spartans"));
        Assert.Equal(["hawaii", "rainbow", "warriors"], CfbGameMatchingService.Tokenize("Hawai'i Rainbow Warriors"));
        Assert.Equal(["texas", "a", "and", "m", "aggies"], CfbGameMatchingService.Tokenize("Texas A&M Aggies"));
    }

    [Fact]
    public void Tokenize_HyphenAndSpaceAreEquivalent()
    {
        Assert.Equal(
            CfbGameMatchingService.Tokenize("Arkansas-Pine Bluff Golden Lions"),
            CfbGameMatchingService.Tokenize("Arkansas Pine Bluff Golden Lions"));
    }

    // -----------------------------------------------------------------------
    // IsSameTeam — names the two APIs write differently
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Ohio State Buckeyes", "Ohio State Buckeyes")]
    [InlineData("San Jose State Spartans", "San José State Spartans")]
    [InlineData("Appalachian State Mountaineers", "App State Mountaineers")]
    [InlineData("Southern Mississippi Golden Eagles", "Southern Miss Golden Eagles")]
    [InlineData("Delaware Blue Hens", "Delaware Fightin' Blue Hens")]
    [InlineData("Presbyterian College Blue Hose", "Presbyterian Blue Hose")]
    [InlineData("Sam Houston State Bearkats", "Sam Houston Bearkats")]
    [InlineData("Yale University Bulldogs", "Yale Bulldogs")]
    [InlineData("Citadel Bulldogs", "The Citadel Bulldogs")]
    [InlineData("Hawaii Rainbow Warriors", "Hawai'i Rainbow Warriors")]
    public void IsSameTeam_MatchesNameVariants(string oddsName, string highlightlyName)
    {
        Assert.True(CfbGameMatchingService.IsSameTeam(oddsName, highlightlyName));
    }

    [Theory]
    [InlineData("Albany", "UAlbany Great Danes")]
    [InlineData("East Texas A&M Lions", "Texas A&M-Commerce Lions")]
    [InlineData("Houston Baptist Huskies", "Houston Christian Huskies")]
    [InlineData("LIU Sharks", "Long Island University Sharks")]
    [InlineData("Southeastern Louisiana Lions", "SE Louisiana Lions")]
    [InlineData("St. Thomas (MN) Tommies", "St. Thomas-Minnesota Tommies")]
    [InlineData("UMass Minutemen", "Massachusetts Minutemen")]
    [InlineData("Youngstown St Penguins", "Youngstown State Penguins")]
    public void IsSameTeam_MatchesRenamedSchoolsViaAlias(string oddsName, string highlightlyName)
    {
        Assert.True(CfbGameMatchingService.IsSameTeam(oddsName, highlightlyName));
    }

    // These pairs share words but are different schools — the exact traps a loose
    // fuzzy match falls into.
    [Theory]
    [InlineData("Duke Blue Devils", "Bluefield Bluefields")]
    [InlineData("South Alabama Jaguars", "Southern Jaguars")]
    [InlineData("Mississippi Valley State Delta Devils", "Delta State Statesmen")]
    [InlineData("Miami Hurricanes", "Miami (OH) RedHawks")]
    [InlineData("Wisconsin Badgers", "Wisconsin-Platteville Pioneers")]
    [InlineData("Texas Longhorns", "Texas State Bobcats")]
    [InlineData("Alabama Crimson Tide", "Alabama State Hornets")]
    [InlineData("Albany", "Albany State Golden Rams")]
    public void IsSameTeam_RejectsDifferentSchoolsWithSharedWords(string oddsName, string highlightlyName)
    {
        Assert.False(CfbGameMatchingService.IsSameTeam(oddsName, highlightlyName));
    }

    // -----------------------------------------------------------------------
    // FindCandidates
    // -----------------------------------------------------------------------

    // 2026-09-20T03:00Z is 11:00 PM EDT on Sept 19 — a Saturday night kickoff that is
    // already "Sunday" in UTC.
    private static readonly DateTimeOffset SaturdayNightKickoff = new(2026, 9, 20, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Saturday = new(2026, 9, 19);

    [Fact]
    public void FindCandidates_MatchesOnEasternDateNotUtcDate()
    {
        var events = new[] { Event("e1", "San Jose State Spartans", "Fresno State Bulldogs", SaturdayNightKickoff) };

        var result = CfbGameMatchingService.FindCandidates(
            "San José State Spartans", "Fresno State Bulldogs", Saturday, events, GetEasternTime());

        var candidate = Assert.Single(result);
        Assert.Equal("e1", candidate.Event.Id);
        Assert.False(candidate.Swapped);
    }

    [Fact]
    public void FindCandidates_NeutralSiteWithFlippedHomeAway_IsMarkedSwapped()
    {
        // Odds API calls Fresno State the home team; Highlightly says San José State is.
        var events = new[] { Event("e1", "Fresno State Bulldogs", "San Jose State Spartans", SaturdayNightKickoff) };

        var result = CfbGameMatchingService.FindCandidates(
            "San José State Spartans", "Fresno State Bulldogs", Saturday, events, GetEasternTime());

        var candidate = Assert.Single(result);
        Assert.True(candidate.Swapped);
    }

    [Fact]
    public void FindCandidates_EventOnADifferentEasternDate_DoesNotMatch()
    {
        var events = new[] { Event("e1", "San Jose State Spartans", "Fresno State Bulldogs", SaturdayNightKickoff) };

        var result = CfbGameMatchingService.FindCandidates(
            "San José State Spartans", "Fresno State Bulldogs", Saturday.AddDays(1), events, GetEasternTime());

        Assert.Empty(result);
    }

    [Fact]
    public void FindCandidates_OnlyOneTeamMatching_DoesNotMatch()
    {
        var events = new[] { Event("e1", "San Jose State Spartans", "Boise State Broncos", SaturdayNightKickoff) };

        var result = CfbGameMatchingService.FindCandidates(
            "San José State Spartans", "Fresno State Bulldogs", Saturday, events, GetEasternTime());

        Assert.Empty(result);
    }

    [Fact]
    public void FindCandidates_SameGameListedTwice_ReturnsBothSoCallerCanSkipAsAmbiguous()
    {
        var events = new[]
        {
            Event("e1", "San Jose State Spartans", "Fresno State Bulldogs", SaturdayNightKickoff),
            Event("e2", "San Jose State Spartans", "Fresno State Bulldogs", SaturdayNightKickoff),
        };

        var result = CfbGameMatchingService.FindCandidates(
            "San José State Spartans", "Fresno State Bulldogs", Saturday, events, GetEasternTime());

        Assert.Equal(2, result.Count);
    }
}
