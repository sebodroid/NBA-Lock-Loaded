using NbaTracker.Data.Entities;
using NbaTracker.Worker.Models.OddsApi;
using NbaTracker.Worker.Services;

namespace NbaTracker.Worker.Tests;

// The Odds API and Highlightly can disagree on which team is "home" — routine for
// neutral-site and bowl games. These pin down that spread, favorite and per-side odds
// follow the TEAM, not the home/away position.
public class CfbLineMappingTests
{
    private const int HomeId = 10;   // the game's home team, per Highlightly
    private const int AwayId = 20;

    private static Game GameRow() => new() { Id = 1, HomeTeamId = HomeId, AwayTeamId = AwayId };

    // Odds API event: "Alpha" is listed as its home team, "Beta" as its away team.
    private static OddsApiEvent Event() => new() { Id = "e1", HomeTeam = "Alpha", AwayTeam = "Beta" };

    private static OddsApiBookmaker Book(string favorite, decimal favPoint, int favPrice, int dogPrice)
    {
        var underdog = favorite == "Alpha" ? "Beta" : "Alpha";
        return new OddsApiBookmaker
        {
            Key = "fanduel",
            Markets =
            [
                new OddsApiMarket
                {
                    Key = "spreads",
                    Outcomes =
                    [
                        new OddsApiOutcome { Name = favorite, Point = -favPoint, Price = favPrice },
                        new OddsApiOutcome { Name = underdog, Point = favPoint, Price = dogPrice },
                    ]
                },
                new OddsApiMarket
                {
                    Key = "totals",
                    Outcomes =
                    [
                        new OddsApiOutcome { Name = "Over", Point = 55.5m, Price = -108 },
                        new OddsApiOutcome { Name = "Under", Point = 55.5m, Price = -112 },
                    ]
                },
            ]
        };
    }

    [Fact]
    public void NotSwapped_FavoriteIsOddsHome_FavoriteIsGameHome()
    {
        var line = CfbSyncOrchestrator.BuildLine(GameRow(), Event(), swapped: false, Book("Alpha", 7.5m, -115, -105));

        Assert.Equal(7.5m, line.Spread);
        Assert.Equal(HomeId, line.FavoriteTeamId);
        Assert.Equal(-115, line.HomeSpreadOdds);
        Assert.Equal(-105, line.AwaySpreadOdds);
    }

    [Fact]
    public void NotSwapped_FavoriteIsOddsAway_FavoriteIsGameAway()
    {
        var line = CfbSyncOrchestrator.BuildLine(GameRow(), Event(), swapped: false, Book("Beta", 3m, -120, 100));

        Assert.Equal(AwayId, line.FavoriteTeamId);
        Assert.Equal(100, line.HomeSpreadOdds);
        Assert.Equal(-120, line.AwaySpreadOdds);
    }

    [Fact]
    public void Swapped_FavoriteIsOddsHome_FavoriteIsGameAway()
    {
        // Odds API's home (Alpha) is Highlightly's away team — so Alpha favored means
        // the game's AWAY team is favored, and its odds belong on the away side.
        var line = CfbSyncOrchestrator.BuildLine(GameRow(), Event(), swapped: true, Book("Alpha", 7.5m, -115, -105));

        Assert.Equal(AwayId, line.FavoriteTeamId);
        Assert.Equal(-105, line.HomeSpreadOdds);
        Assert.Equal(-115, line.AwaySpreadOdds);
    }

    [Fact]
    public void Swapped_FavoriteIsOddsAway_FavoriteIsGameHome()
    {
        var line = CfbSyncOrchestrator.BuildLine(GameRow(), Event(), swapped: true, Book("Beta", 3m, -120, 100));

        Assert.Equal(HomeId, line.FavoriteTeamId);
        Assert.Equal(-120, line.HomeSpreadOdds);
        Assert.Equal(100, line.AwaySpreadOdds);
    }

    [Fact]
    public void Totals_AreIndependentOfOrientation()
    {
        var plain = CfbSyncOrchestrator.BuildLine(GameRow(), Event(), swapped: false, Book("Alpha", 7.5m, -115, -105));
        var flipped = CfbSyncOrchestrator.BuildLine(GameRow(), Event(), swapped: true, Book("Alpha", 7.5m, -115, -105));

        Assert.Equal(55.5m, plain.Total);
        Assert.Equal(-108, plain.OverOdds);
        Assert.Equal(-112, plain.UnderOdds);
        Assert.Equal(plain.Total, flipped.Total);
        Assert.Equal(plain.OverOdds, flipped.OverOdds);
    }

    [Fact]
    public void NoSpreadsMarket_LeavesSpreadAndFavoriteUnset_ButKeepsTotal()
    {
        var book = Book("Alpha", 7.5m, -115, -105);
        book.Markets.RemoveAll(m => m.Key == "spreads");

        var line = CfbSyncOrchestrator.BuildLine(GameRow(), Event(), swapped: false, book);

        Assert.Null(line.Spread);
        Assert.Null(line.FavoriteTeamId);
        Assert.Equal(55.5m, line.Total);
    }

    [Fact]
    public void FavoriteNameMatchingNeitherTeam_LeavesFavoriteUnset()
    {
        var line = CfbSyncOrchestrator.BuildLine(GameRow(), Event(), swapped: false, Book("Gamma", 7.5m, -115, -105));

        Assert.Equal(7.5m, line.Spread);
        Assert.Null(line.FavoriteTeamId);
        Assert.Null(line.HomeSpreadOdds);
    }
}
