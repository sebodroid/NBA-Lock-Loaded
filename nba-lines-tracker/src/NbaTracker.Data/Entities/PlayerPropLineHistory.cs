namespace NbaTracker.Data.Entities;

// Append-only — one row per sync pass, never overwritten (unlike PlayerPropLine itself,
// which holds only the current value). PlayerPropLine's own history is destroyed on
// every sync; this is the record of how a line actually moved, which is one of the
// strongest signals in sharp betting (a line moving against the public is often sharper
// than any box-score-derived stat) but only exists going forward from whenever capture
// starts — there is no way to reconstruct it retroactively, so every day this doesn't
// run is lost data.
public class PlayerPropLineHistory
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int PlayerId { get; set; }
    public string MarketKey { get; set; } = null!;
    public decimal Line { get; set; }
    public int? OverOdds { get; set; }
    public int? UnderOdds { get; set; }
    public string Bookmaker { get; set; } = null!;
    public string? UnderBookmaker { get; set; }
    public DateTime RecordedAt { get; set; }

    public Game Game { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
