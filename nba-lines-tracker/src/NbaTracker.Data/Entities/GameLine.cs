namespace NbaTracker.Data.Entities;

public class GameLine
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public decimal? Spread { get; set; }                 // absolute value from favorite's perspective
    public int? FavoriteTeamId { get; set; }             // explicit FK — never infer favorite from spread sign
    public decimal? Total { get; set; }
    public int? HomeSpreadOdds { get; set; }             // American odds (e.g. -110)
    public int? AwaySpreadOdds { get; set; }
    public int? OverOdds { get; set; }
    public int? UnderOdds { get; set; }
    public string? Bookmaker { get; set; }               // "fandraft" = canonical; "hardrockbet" = fallback
    public DateTime? LineTimestamp { get; set; }         // UTC timestamp of line snapshot
    public DateTime UpdatedAt { get; set; }

    public Game Game { get; set; } = null!;
    public Team? FavoriteTeam { get; set; }
}
