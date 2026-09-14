namespace NbaTracker.Data.Entities;

// Caches the Claude-generated preview text for a game so it isn't regenerated
// on every page view — only when stale (see AiPreviewService.IsStale).
public class GamePreview
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string Text { get; set; } = null!;
    public DateTime GeneratedAt { get; set; }

    public Game Game { get; set; } = null!;
}
