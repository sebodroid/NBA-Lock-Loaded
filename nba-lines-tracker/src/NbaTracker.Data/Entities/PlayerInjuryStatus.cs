namespace NbaTracker.Data.Entities;

// One row per player currently listed on an injury report — fully replaced on every
// sync (not appended), and only for non-"Active" statuses, so a row existing at all
// means "something's worth flagging." Sourced from ESPN's public injuries API (no
// auth needed), matched to our Player rows by name — see EspnInjuryClient.
public class PlayerInjuryStatus
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string Status { get; set; } = null!;   // "Questionable" | "Doubtful" | "Out" | "Injured Reserve"
    public string? Note { get; set; }              // ESPN's short comment, e.g. "ankle injury"
    public DateTime UpdatedAt { get; set; }

    public Player Player { get; set; } = null!;
}
