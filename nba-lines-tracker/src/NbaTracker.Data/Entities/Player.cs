namespace NbaTracker.Data.Entities;

public class Player
{
    public int Id { get; set; }
    public string ExternalId { get; set; } = null!;   // Highlightly player ID
    public string Name { get; set; } = null!;
    public int? Jersey { get; set; }
    public string Sport { get; set; } = "NFL";
    public int? TeamId { get; set; }                  // last known team, updated as new games are captured
    public DateTime UpdatedAt { get; set; }

    public Team? Team { get; set; }
    public ICollection<PlayerGameStat> GameStats { get; set; } = [];
}
