namespace NbaTracker.Data.Entities;

public enum SyncRunStatus { Running, Success, Partial, Failed }

public class SyncRun
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public SyncRunStatus Status { get; set; }
    public int? GamesProcessed { get; set; }
    public string? ErrorDetails { get; set; }            // JSON blob of error info
    public string? Notes { get; set; }
}
