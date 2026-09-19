using System.Text.Json;
using NbaTracker.Data.Entities;
using NbaTracker.Worker.Models.OddsApi;
using NbaTracker.Worker.Services;

namespace NbaTracker.Worker.Tests;

public class CfbTeamPropsTests
{
    // Trimmed from a real Odds API event-odds response (Florida @ Auburn, Sept 19 2026).
    // Books cover different markets: only FanDuel has team rushing yards, only DraftKings
    // has first-half team totals — which is why the mapper picks a book per market.
    private const string Payload = """
    {
      "id": "7ef3b27d15de635a3e05324f2b0c56cb",
      "sport_key": "americanfootball_ncaaf",
      "commence_time": "2026-09-19T23:00:00Z",
      "home_team": "Auburn Tigers",
      "away_team": "Florida Gators",
      "bookmakers": [
        { "key": "fanduel", "title": "FanDuel", "markets": [
          { "key": "team_totals", "outcomes": [
            { "name": "Over",  "description": "Auburn Tigers",  "price": -113, "point": 26.5 },
            { "name": "Under", "description": "Auburn Tigers",  "price": -113, "point": 26.5 },
            { "name": "Under", "description": "Florida Gators", "price": -125, "point": 27.5 },
            { "name": "Over",  "description": "Florida Gators", "price": -102, "point": 27.5 } ] },
          { "key": "team_total_rush_yds", "outcomes": [
            { "name": "Over",  "description": "Auburn Tigers",  "price": -114, "point": 153.5 },
            { "name": "Under", "description": "Auburn Tigers",  "price": -114, "point": 153.5 } ] },
          { "key": "spreads_h1", "outcomes": [
            { "name": "Auburn Tigers",  "price": -128, "point": 1.5 },
            { "name": "Florida Gators", "price": 104,  "point": -1.5 } ] },
          { "key": "totals_h1", "outcomes": [
            { "name": "Over",  "price": -122, "point": 26.5 },
            { "name": "Under", "price": 100,  "point": 26.5 } ] },
          { "key": "h2h_h1", "outcomes": [
            { "name": "Auburn Tigers",  "price": 100 },
            { "name": "Florida Gators", "price": -122 } ] }
        ] },
        { "key": "draftkings", "title": "DraftKings", "markets": [
          { "key": "team_totals_h1", "outcomes": [
            { "name": "Over",  "description": "Auburn Tigers",  "price": -105, "point": 13.5 },
            { "name": "Under", "description": "Auburn Tigers",  "price": -125, "point": 13.5 } ] },
          { "key": "h2h_h1", "outcomes": [
            { "name": "Auburn Tigers",  "price": 105 },
            { "name": "Florida Gators", "price": -125 } ] }
        ] }
      ]
    }
    """;

    private static readonly string[] Priority = ["fanduel", "hardrockbet", "draftkings"];

    private static OddsApiEvent Event() => JsonSerializer.Deserialize<OddsApiEvent>(Payload)!;

    // Auburn is the game's home team, matching the Odds API's own home team.
    private static Game AuburnHome() => new() { Id = 1, HomeTeamId = 10, AwayTeamId = 20 };

    private static List<TeamPropLine> ForMarket(List<TeamPropLine> lines, string key) =>
        lines.Where(l => l.MarketKey == key).ToList();

    [Fact]
    public void EachMarketUsesTheFirstBookmakerThatPostsIt()
    {
        var lines = CfbTeamProps.BuildLines(AuburnHome(), Event(), swapped: false, Priority);

        // FanDuel and DraftKings both post h2h_h1 — priority order picks FanDuel.
        Assert.All(ForMarket(lines, "h2h_h1"), l => Assert.Equal("fanduel", l.Bookmaker));
        // Only DraftKings has first-half team totals.
        Assert.All(ForMarket(lines, "team_totals_h1"), l => Assert.Equal("draftkings", l.Bookmaker));
        // Only FanDuel has team rushing yards.
        Assert.All(ForMarket(lines, "team_total_rush_yds"), l => Assert.Equal("fanduel", l.Bookmaker));
    }

    [Fact]
    public void TeamTotals_AreOneOverAndUnderPerTeam()
    {
        var lines = ForMarket(CfbTeamProps.BuildLines(AuburnHome(), Event(), swapped: false, Priority), "team_totals");

        Assert.Equal(4, lines.Count);
        var auburnOver = Assert.Single(lines, l => l.TeamId == 10 && l.Side == "Over");
        Assert.Equal(26.5m, auburnOver.Line);
        Assert.Equal(-113, auburnOver.Price);
        var floridaUnder = Assert.Single(lines, l => l.TeamId == 20 && l.Side == "Under");
        Assert.Equal(27.5m, floridaUnder.Line);
        Assert.Equal(-125, floridaUnder.Price);
    }

    [Fact]
    public void GameLevelTotals_HaveNoTeam()
    {
        var lines = ForMarket(CfbTeamProps.BuildLines(AuburnHome(), Event(), swapped: false, Priority), "totals_h1");

        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Null(l.TeamId));
        Assert.Equal(26.5m, lines.First(l => l.Side == "Over").Line);
    }

    [Fact]
    public void Spreads_KeepTheSignedLineForEachTeam_AndMoneylinesHaveNone()
    {
        var all = CfbTeamProps.BuildLines(AuburnHome(), Event(), swapped: false, Priority);

        var spreads = ForMarket(all, "spreads_h1");
        Assert.Equal(1.5m, Assert.Single(spreads, l => l.TeamId == 10).Line);
        Assert.Equal(-1.5m, Assert.Single(spreads, l => l.TeamId == 20).Line);
        Assert.All(spreads, l => Assert.Equal("Team", l.Side));

        var moneylines = ForMarket(all, "h2h_h1");
        Assert.All(moneylines, l => Assert.Null(l.Line));
        Assert.Equal(100, Assert.Single(moneylines, l => l.TeamId == 10).Price);
    }

    [Fact]
    public void Swapped_MapsOddsApiTeamsToTheOppositeGameSide()
    {
        // Highlightly has Florida at home (id 10) and Auburn away (id 20) — the reverse
        // of the Odds API — so "Auburn Tigers" must resolve to the game's away team.
        var game = new Game { Id = 1, HomeTeamId = 10, AwayTeamId = 20 };

        var lines = ForMarket(CfbTeamProps.BuildLines(game, Event(), swapped: true, Priority), "team_totals");

        Assert.Equal(26.5m, Assert.Single(lines, l => l.TeamId == 20 && l.Side == "Over").Line);   // Auburn
        Assert.Equal(27.5m, Assert.Single(lines, l => l.TeamId == 10 && l.Side == "Over").Line);   // Florida
    }

    [Fact]
    public void MarketWithAnUnresolvableTeam_IsDroppedWhole_OthersSurvive()
    {
        var evt = Event();
        evt.Bookmakers[0].Markets.First(m => m.Key == "team_totals").Outcomes[0].Description = "Some Other School";

        var lines = CfbTeamProps.BuildLines(AuburnHome(), evt, swapped: false, Priority);

        Assert.Empty(ForMarket(lines, "team_totals"));
        Assert.NotEmpty(ForMarket(lines, "spreads_h1"));
    }

    [Fact]
    public void OverUnderWithoutALine_DropsThatMarket()
    {
        var evt = Event();
        evt.Bookmakers[0].Markets.First(m => m.Key == "totals_h1").Outcomes[0].Point = null;

        var lines = CfbTeamProps.BuildLines(AuburnHome(), evt, swapped: false, Priority);

        Assert.Empty(ForMarket(lines, "totals_h1"));
    }

    [Fact]
    public void MarketsNoBookPosts_ProduceNoRows()
    {
        var lines = CfbTeamProps.BuildLines(AuburnHome(), Event(), swapped: false, Priority);

        // The payload has no quarter markets at all.
        Assert.Empty(ForMarket(lines, "spreads_q1"));
        Assert.Empty(ForMarket(lines, "totals_q1"));
    }

    [Fact]
    public void BookmakerOutsideThePriorityList_IsIgnored()
    {
        var lines = CfbTeamProps.BuildLines(AuburnHome(), Event(), swapped: false, ["betmgm"]);

        Assert.Empty(lines);
    }
}
