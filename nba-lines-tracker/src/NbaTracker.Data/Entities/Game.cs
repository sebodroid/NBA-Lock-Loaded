namespace NbaTracker.Data.Entities;

public class Game
{
    public int Id { get; set; }
    public string NbaGameId { get; set; } = null!;      // BallDontLie game ID
    public string? OddsApiGameId { get; set; }           // The Odds API event ID for cross-API matching
    public DateOnly GameDate { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public int? HomeScore { get; set; }                  // null until final
    public int? AwayScore { get; set; }
    public string Status { get; set; } = null!;          // SCHEDULED / LIVE / FINAL / POSTPONED
    public string Season { get; set; } = null!;          // e.g. "2024-25"
    public bool? WentToOvertime { get; set; }            // required for O/U push detection
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;
    public GameLine? GameLine { get; set; }
    public GameResult? GameResult { get; set; }
}
