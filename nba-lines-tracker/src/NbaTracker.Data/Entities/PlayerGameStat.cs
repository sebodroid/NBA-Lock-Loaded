namespace NbaTracker.Data.Entities;

// Flexible name/value shape — mirrors Highlightly's own box score structure
// (group/name/value triples) rather than fixed columns per stat, since the
// full stat vocabulary isn't known until real data flows through.
public class PlayerGameStat
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public int GameId { get; set; }
    public int TeamId { get; set; }         // team the player suited up for in this specific game
    public string Category { get; set; } = null!;   // "Passing" / "Rushing" / "Receiving" / "Defense" / ...
    public string StatName { get; set; } = null!;    // verbatim from Highlightly, e.g. "Total Successful Passes"
    public decimal? Value { get; set; }              // parsed numeric value, null if not parseable
    public string RawValue { get; set; } = null!;     // original value as returned, for anything non-numeric
    public DateTime CreatedAt { get; set; }

    public Player Player { get; set; } = null!;
    public Game Game { get; set; } = null!;
    public Team Team { get; set; } = null!;
}
