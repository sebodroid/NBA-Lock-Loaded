namespace NbaTracker.Data.Entities;

public class Team
{
    public int Id { get; set; }
    public string NbaApiId { get; set; } = null!;    // BallDontLie team ID — separate from PK
    public string Name { get; set; } = null!;
    public string Abbreviation { get; set; } = null!;
    public string? Conference { get; set; }
    public string? Division { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Game> HomeGames { get; set; } = [];
    public ICollection<Game> AwayGames { get; set; } = [];
}
