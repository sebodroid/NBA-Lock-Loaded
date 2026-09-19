namespace NbaTracker.Data.Entities;

// One row per betting outcome of a team-level market (college football has team props
// but no player props). Outcomes vary by market — a first-half spread is one row per
// team, a team total is an Over and an Under row per team, a game-level first-half
// total is an Over and an Under row with no team — so a row is the outcome, not the market.
public class TeamPropLine
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string MarketKey { get; set; } = null!;    // verbatim from The Odds API, e.g. "team_totals"
    public string Side { get; set; } = null!;         // "Over" | "Under" | "Team" (spread/moneyline outcome)
    public int? TeamId { get; set; }                  // team this outcome is for; null for game-level totals
    public decimal? Line { get; set; }                // signed for spreads; null for moneylines
    public int Price { get; set; }                    // American odds
    public string Bookmaker { get; set; } = null!;
    public DateTime LineTimestamp { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Game Game { get; set; } = null!;
    public Team? Team { get; set; }
}
