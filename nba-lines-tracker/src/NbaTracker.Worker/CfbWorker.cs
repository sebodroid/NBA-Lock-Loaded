using Cronos;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using NbaTracker.Data;
using NbaTracker.Data.Entities;
using NbaTracker.Worker.Services;

namespace NbaTracker.Worker;

public class CfbWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SyncOptions _options;
    private readonly ILogger<CfbWorker> _logger;

    // College games are mostly Saturdays but also land on Thursday/Friday nights, a
    // Week 0 the weekend before the season "starts", and midweek MACtion. Like the NFL
    // worker, every cron fire re-syncs a trailing window instead of guessing which
    // weekdays had games — self-healing, and Saturday's late finishes get picked up.
    private const int RollingWindowDays = 7;

    // Fires after NBA (5 AM), MLB (6 AM) and NFL (7 AM). Saturday night games are
    // long final by 8 AM ET.
    private static readonly CronExpression DailySchedule =
        CronExpression.Parse("0 8 * * *", CronFormat.Standard);

    private static readonly TimeZoneInfo EasternTime =
        TimeZoneInfo.FindSystemTimeZoneById(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Eastern Standard Time"
                : "America/New_York");

    public CfbWorker(
        IServiceScopeFactory scopeFactory,
        SyncOptions options,
        ILogger<CfbWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.CfbHistoricalSeason.HasValue)
        {
            await RunHistoricalSeasonBackfillAsync(_options.CfbHistoricalSeason.Value, stoppingToken);
            return;
        }

        if (_options.SyncDate.HasValue)
        {
            _logger.LogInformation("[CFB] Single-date mode: syncing {Date}", _options.SyncDate.Value);
            await RunSyncForDateAsync(_options.SyncDate.Value, stoppingToken);
            return;
        }

        if (_options.IsBackfill)
        {
            await RunBackfillAsync(stoppingToken);
            return;
        }

        await RunGapDetectionAsync(stoppingToken);
        await RunRollingWindowAsync(stoppingToken);
        await RunScheduleLoopAsync(stoppingToken);
    }

    private async Task RunScheduleLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var next = DailySchedule.GetNextOccurrence(now, EasternTime);
            if (next is null) break;

            var delay = next.Value - now;
            _logger.LogInformation("[CFB] Next sync scheduled at {Next} ET", next.Value);
            await Task.Delay(delay, ct);
            if (ct.IsCancellationRequested) break;

            await RunRollingWindowAsync(ct);
        }
    }

    /// <summary>
    /// Re-syncs the last RollingWindowDays calendar days (through today), oldest first.
    /// Upserts are idempotent and the orchestrator skips the paid Odds API calls for
    /// dates that are already fully settled, so re-covering recent days daily is cheap.
    /// </summary>
    private async Task RunRollingWindowAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(-(RollingWindowDays - 1));

        _logger.LogInformation("[CFB] Rolling window sync: {Start} to {End}", start, today);

        for (var d = start; d <= today; d = d.AddDays(1))
        {
            await RunSyncForDateAsync(d, ct);
            if (ct.IsCancellationRequested) return;
        }
    }

    private async Task RunGapDetectionAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NbaTrackerDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Don't backfill before the season actually starts
        var seasonStart = SportSeasonBoundaries.CfbRegularSeasonStart;

        var lastSyncedDate = await db.SyncRuns
            .Where(r => r.Sport == "CFB"
                     && r.SyncDate != null
                     && (r.Status == SyncRunStatus.Success || r.Status == SyncRunStatus.Partial))
            .OrderByDescending(r => r.SyncDate)
            .Select(r => r.SyncDate)
            .FirstOrDefaultAsync(ct);

        if (lastSyncedDate is null)
        {
            _logger.LogInformation("[CFB] No previous sync found — skipping gap backfill");
            return;
        }

        // Only need to cover the gap older than the rolling window — the window itself
        // gets covered by RunRollingWindowAsync right after this.
        var windowStart = today.AddDays(-(RollingWindowDays - 1));
        for (var d = lastSyncedDate.Value.AddDays(1); d < windowStart; d = d.AddDays(1))
        {
            if (d < seasonStart) continue;
            _logger.LogInformation("[CFB] Gap detected: syncing {Date}", d);
            await RunSyncForDateAsync(d, ct);
            if (ct.IsCancellationRequested) return;
        }
    }

    private async Task RunBackfillAsync(CancellationToken ct)
    {
        var start = SportSeasonBoundaries.CfbRegularSeasonStart;
        var end = DateOnly.FromDateTime(DateTime.UtcNow);

        _logger.LogInformation("[CFB] Backfill mode: {Start} to {End}", start, end);

        for (var d = start; d <= end; d = d.AddDays(1))
        {
            await RunSyncForDateAsync(d, ct);
            if (ct.IsCancellationRequested) break;
        }

        _logger.LogInformation("[CFB] Backfill complete");
    }

    /// <summary>
    /// One-time backfill of a completed past season (e.g. CFB_HISTORICAL_SEASON=2025),
    /// bounded by that season's own start/end dates from SportSeasonBoundaries. Expect
    /// this to spend real Odds API credits: every date with games costs a historical
    /// request per sport key (FBS and FCS), and a full season has roughly 60 such dates.
    /// </summary>
    private async Task RunHistoricalSeasonBackfillAsync(int season, CancellationToken ct)
    {
        if (!SportSeasonBoundaries.CfbRegularSeasonStarts.TryGetValue(season, out var start))
        {
            _logger.LogError(
                "[CFB] No known regular season start date for {Season} — add it to SportSeasonBoundaries first",
                season);
            return;
        }
        if (!SportSeasonBoundaries.CfbSeasonEnds.TryGetValue(season, out var end))
        {
            _logger.LogError(
                "[CFB] No known season end date for {Season} — add it to SportSeasonBoundaries first",
                season);
            return;
        }

        _logger.LogInformation("[CFB] Historical {Season} season backfill: {Start} to {End}", season, start, end);

        for (var d = start; d <= end; d = d.AddDays(1))
        {
            await RunSyncForDateAsync(d, ct);
            if (ct.IsCancellationRequested) break;
        }

        _logger.LogInformation("[CFB] Historical {Season} season backfill complete", season);
    }

    private async Task RunSyncForDateAsync(DateOnly date, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<CfbSyncOrchestrator>();
        await orchestrator.RunDailySyncAsync(date, ct);
    }
}
