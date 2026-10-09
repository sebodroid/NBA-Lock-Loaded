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

    // The best price for each side is shopped independently across every trusted book
    // (see NflSyncOrchestrator.SyncPlayerPropsAsync), so they can legitimately come from
    // two different books — Bookmaker is the Over side's book, UnderBookmaker the Under's.
    public string Bookmaker { get; set; } = null!;
    public string? UnderBookmaker { get; set; }
    public DateTime LineTimestamp { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Game Game { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
