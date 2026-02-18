namespace NbaTracker.Data.Entities;

// 3-value enums — boolean would corrupt push-case analytics
public enum AtsResult { Cover, Loss, Push }
public enum OuResult { Over, Under, Push }

public class GameResult
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public AtsResult? HomeAtsResult { get; set; }       // null if no line was available
    public AtsResult? AwayAtsResult { get; set; }
    public OuResult? OuResult { get; set; }             // null if no total was available
    public DateTime? ResolvedAt { get; set; }

    public Game Game { get; set; } = null!;
}
