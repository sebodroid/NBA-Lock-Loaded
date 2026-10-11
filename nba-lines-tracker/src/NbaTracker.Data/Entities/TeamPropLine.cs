namespace NbaTracker.Data.Entities;

public class TeamPropLine
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string MarketKey { get; set; } = null!;
    public string Side { get; set; } = null!;
    public int? TeamId { get; set; }
    public decimal? Line { get; set; }
    public int Price { get; set; }
    public string Bookmaker { get; set; } = null!;
    public DateTime LineTimestamp { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Game Game { get; set; } = null!;
    public Team? Team { get; set; }
}
