using System.Text;

namespace NbaTracker.Worker.Services;

/// <summary>
/// Writes structured log files for each sync run:
///   - /logs/sync-YYYY-MM-DD.log  — one entry per DB write for that date
///   - /logs/failed-days.log      — one line per day that ended Partial or Failed
/// </summary>
public class SyncFileLogger
{
    private readonly string _logPath;
    private readonly object _lock = new();

    public SyncFileLogger(IConfiguration config)
    {
        _logPath = config["SyncLog:Path"] ?? "/logs";
        Directory.CreateDirectory(_logPath);
    }

    // -----------------------------------------------------------------------
    // Lifecycle entries
    // -----------------------------------------------------------------------

    public void LogSyncStart(DateOnly date)
        => AppendDaily(date, FormatLine("SYNC_START", $"date={date:yyyy-MM-dd}"));

    public void LogSyncComplete(DateOnly date, string status, int gamesProcessed, int errorCount, string? errorDetails = null)
    {
        var line = FormatLine("SYNC_COMPLETE",
            $"date={date:yyyy-MM-dd}  status={status}  games_processed={gamesProcessed}  errors={errorCount}");
        AppendDaily(date, line);

        // Mirror to the failures log when something went wrong
        if (status is "Partial" or "Failed")
        {
            var detail = errorDetails is not null ? $"  detail={Sanitize(errorDetails)}" : string.Empty;
            var failLine = FormatLine(status.ToUpperInvariant(),
                $"date={date:yyyy-MM-dd}  games_processed={gamesProcessed}  errors={errorCount}{detail}");
            AppendFailed(failLine);
        }
    }

    // -----------------------------------------------------------------------
    // DB write entries
    // -----------------------------------------------------------------------

    public void LogGameWriteOk(DateOnly date, string nbaGameId, string status)
        => AppendDaily(date, FormatLine("DB_WRITE_OK",
            $"entity=Game        nba_game_id={nbaGameId}  status={status}"));

    public void LogGameWriteFail(DateOnly date, string nbaGameId, string error)
        => AppendDaily(date, FormatLine("DB_WRITE_FAIL",
            $"entity=Game        nba_game_id={nbaGameId}  error={Sanitize(error)}"));

    public void LogLineWriteOk(DateOnly date, int gameId, decimal? spread, decimal? total, string? bookmaker)
        => AppendDaily(date, FormatLine("DB_WRITE_OK",
            $"entity=GameLine    game_id={gameId}  spread={spread?.ToString() ?? "null"}  total={total?.ToString() ?? "null"}  bookmaker={bookmaker ?? "null"}"));

    public void LogResultWriteOk(DateOnly date, int gameId, string homeAts, string awayAts, string ouResult)
        => AppendDaily(date, FormatLine("DB_WRITE_OK",
            $"entity=GameResult  game_id={gameId}  home_ats={homeAts}  away_ats={awayAts}  ou={ouResult}"));

    public void LogLineWriteFail(DateOnly date, int gameId, string error)
        => AppendDaily(date, FormatLine("DB_WRITE_FAIL",
            $"entity=GameLine    game_id={gameId}  error={Sanitize(error)}"));

    // -----------------------------------------------------------------------
    // Internals
    // -----------------------------------------------------------------------

    private void AppendDaily(DateOnly date, string line)
    {
        var filePath = Path.Combine(_logPath, $"sync-{date:yyyy-MM-dd}.log");
        lock (_lock)
            File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
    }

    private void AppendFailed(string line)
    {
        var filePath = Path.Combine(_logPath, "failed-days.log");
        lock (_lock)
            File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
    }

    private static string FormatLine(string level, string message)
        => $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] {level,-15} {message}";

    private static string Sanitize(string s)
        => s.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
}
