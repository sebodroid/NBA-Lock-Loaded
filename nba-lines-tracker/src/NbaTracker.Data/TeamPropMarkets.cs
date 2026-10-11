namespace NbaTracker.Data;

public static class TeamPropMarkets
{
    public record Market(string Key, string Label, string Group);

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
