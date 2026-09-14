namespace NbaTracker.Data.Entities;

public class PlayerPropLine
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int PlayerId { get; set; }
    public string MarketKey { get; set; } = null!;    // e.g. "player_rush_yds" — verbatim from The Odds API
    public decimal Line { get; set; }
    public int? OverOdds { get; set; }
    public int? UnderOdds { get; set; }
    public string Bookmaker { get; set; } = null!;
    public DateTime LineTimestamp { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Game Game { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
