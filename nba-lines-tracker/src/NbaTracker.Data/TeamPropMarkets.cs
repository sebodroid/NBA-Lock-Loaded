namespace NbaTracker.Data;

// The team-level Odds API markets tracked for college football, with how the UI labels
// and groups them. Shared by the Worker (which markets to fetch) and the Api (labels).
//
// This list is also the credit bill: the Odds API charges one credit per distinct market
// returned per game, so a fully covered game costs up to Markets.Count credits and a
// Saturday's ~85 FBS games cost ~700. Trim here to spend less. Alternate lines, the
// other quarters and the odd/even novelty markets exist but are deliberately left out.
public static class TeamPropMarkets
{
    public record Market(string Key, string Label, string Group);

    // Display order within each group is the order listed here.
    public static readonly IReadOnlyList<Market> Markets =
    [
        new("team_totals",         "Team Total",              "Full Game"),
        new("team_total_rush_yds", "Team Rushing Yards",      "Full Game"),
        new("spreads_h1",          "1st Half Spread",         "1st Half"),
        new("totals_h1",           "1st Half Total",          "1st Half"),
        new("h2h_h1",              "1st Half Moneyline",      "1st Half"),
        new("team_totals_h1",      "1st Half Team Total",     "1st Half"),
        new("spreads_q1",          "1st Quarter Spread",      "1st Quarter"),
        new("totals_q1",           "1st Quarter Total",       "1st Quarter"),
    ];

    public static readonly IReadOnlyList<string> Groups = ["Full Game", "1st Half", "1st Quarter"];

    public static IEnumerable<string> Keys => Markets.Select(m => m.Key);

    public static Market? Find(string key) => Markets.FirstOrDefault(m => m.Key == key);
}
