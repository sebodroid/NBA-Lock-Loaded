using Cronos;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using NbaTracker.Data;
using NbaTracker.Data.Entities;
using NbaTracker.Worker.Services;

namespace NbaTracker.Worker;

public class NflWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SyncOptions _options;
    private readonly ILogger<NflWorker> _logger;

    // NFL games land on irregular days — Wednesday openers, Thanksgiving, Christmas,
    // late-season Saturdays — so instead of syncing "today" only, every cron fire
    // re-syncs a trailing window. That makes the worker self-healing regardless of
    // which day(s) actually had games, with no hardcoded weekday assumptions.
    private const int RollingWindowDays = 7;

    // Fires after NBA (5 AM) and MLB (6 AM) — Sunday/Monday/Thursday night games
    // are long final by 7 AM ET.
    private static readonly CronExpression DailySchedule =
        CronExpression.Parse("0 7 * * *", CronFormat.Standard);

    private static readonly TimeZoneInfo EasternTime =
        TimeZoneInfo.FindSystemTimeZoneById(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Eastern Standard Time"
                : "America/New_York");

    public NflWorker(
        IServiceScopeFactory scopeFactory,
        SyncOptions options,
        ILogger<NflWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.NflHistoricalSeason.HasValue)
        {
            await RunHistoricalSeasonBackfillAsync(_options.NflHistoricalSeason.Value, stoppingToken);
            return;
        }

        if (_options.SyncDate.HasValue)
        {
            _logger.LogInformation("[NFL] Single-date mode: syncing {Date}", _options.SyncDate.Value);
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
            _logger.LogInformation("[NFL] Next sync scheduled at {Next} ET", next.Value);
            await Task.Delay(delay, ct);
            if (ct.IsCancellationRequested) break;

            await RunRollingWindowAsync(ct);
        }
    }

    /// <summary>
    /// Re-syncs the last RollingWindowDays calendar days (through today), oldest first.
    /// Upserts are idempotent, so re-covering recent days daily is cheap and catches
    /// games on any day of the week plus late score corrections.
    /// </summary>
    private async Task RunRollingWindowAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(-(RollingWindowDays - 1));

        _logger.LogInformation("[NFL] Rolling window sync: {Start} to {End}", start, today);

        for (var d = start; d <= today; d = d.AddDays(1))
        {
            await RunSyncForDateAsync(d, ct);
            if (ct.IsCancellationRequested) return;
        }

        await RunInjurySyncAsync(ct);
    }

    // Not date-scoped like everything else here — the injury report is a current
    // snapshot, not something tied to a specific day's games — so this runs once per
    // cycle rather than once per date in the rolling window.
    private async Task RunInjurySyncAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<NflSyncOrchestrator>();
        await orchestrator.SyncInjuriesAsync(ct);
    }

    private async Task RunGapDetectionAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NbaTrackerDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Don't backfill before the season actually starts
        var seasonStart = SportSeasonBoundaries.NflRegularSeasonStart;

        var lastSyncedDate = await db.SyncRuns
            .Where(r => r.Sport == "NFL"
                     && r.SyncDate != null
                     && (r.Status == SyncRunStatus.Success || r.Status == SyncRunStatus.Partial))
            .OrderByDescending(r => r.SyncDate)
            .Select(r => r.SyncDate)
            .FirstOrDefaultAsync(ct);

        if (lastSyncedDate is null)
        {
            _logger.LogInformation("[NFL] No previous sync found — skipping gap backfill");
            return;
        }

        // Only need to cover the gap older than the rolling window — the window itself
        // gets covered by RunRollingWindowAsync right after this.
        var windowStart = today.AddDays(-(RollingWindowDays - 1));
        for (var d = lastSyncedDate.Value.AddDays(1); d < windowStart; d = d.AddDays(1))
        {
            if (d < seasonStart) continue;
            _logger.LogInformation("[NFL] Gap detected: syncing {Date}", d);
            await RunSyncForDateAsync(d, ct);
            if (ct.IsCancellationRequested) return;
        }
    }

    private async Task RunBackfillAsync(CancellationToken ct)
    {
        var start = SportSeasonBoundaries.NflRegularSeasonStart;
        var end = DateOnly.FromDateTime(DateTime.UtcNow);

        _logger.LogInformation("[NFL] Backfill mode: {Start} to {End}", start, end);

        for (var d = start; d <= end; d = d.AddDays(1))
        {
            await RunSyncForDateAsync(d, ct);
            if (ct.IsCancellationRequested) break;
        }

        _logger.LogInformation("[NFL] Backfill complete");
    }

    /// <summary>
    /// One-time backfill of a completed past season (e.g. NFL_HISTORICAL_SEASON=2025),
    /// using that season's own start/end dates from SportSeasonBoundaries — bounded so
    /// it can't overreach into the following season's preseason. Player props are not
    /// fetched for historical dates (RunDailySyncAsync only does that for "today"), which
    /// is correct here — there's nothing meaningful to show betting lines for on a game
    /// that already happened.
    /// </summary>
    private async Task RunHistoricalSeasonBackfillAsync(int season, CancellationToken ct)
    {
        if (!SportSeasonBoundaries.NflRegularSeasonStarts.TryGetValue(season, out var start))
        {
            _logger.LogError(
                "[NFL] No known regular season start date for {Season} — add it to SportSeasonBoundaries first",
                season);
            return;
        }
        if (!SportSeasonBoundaries.NflSeasonEnds.TryGetValue(season, out var end))
        {
            _logger.LogError(
                "[NFL] No known season end date for {Season} — add it to SportSeasonBoundaries first",
                season);
            return;
        }

        _logger.LogInformation("[NFL] Historical {Season} season backfill: {Start} to {End}", season, start, end);

        for (var d = start; d <= end; d = d.AddDays(1))
        {
            await RunSyncForDateAsync(d, ct);
            if (ct.IsCancellationRequested) break;
        }

        _logger.LogInformation("[NFL] Historical {Season} season backfill complete", season);
    }

    private async Task RunSyncForDateAsync(DateOnly date, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<NflSyncOrchestrator>();
        await orchestrator.RunDailySyncAsync(date, ct);
    }
}
