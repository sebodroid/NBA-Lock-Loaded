using Cronos;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using NbaTracker.Data;
using NbaTracker.Data.Entities;
using NbaTracker.Worker.Services;

namespace NbaTracker.Worker;

public class MlbWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SyncOptions _options;
    private readonly ILogger<MlbWorker> _logger;

    // MLB games run afternoon through late night ET — sync at 6 AM ET
    // to capture all final scores from the previous day
    private static readonly CronExpression DailySchedule =
        CronExpression.Parse("0 6 * * *", CronFormat.Standard);

    private static readonly TimeZoneInfo EasternTime =
        TimeZoneInfo.FindSystemTimeZoneById(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Eastern Standard Time"
                : "America/New_York");

    public MlbWorker(
        IServiceScopeFactory scopeFactory,
        SyncOptions options,
        ILogger<MlbWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.SyncDate.HasValue)
        {
            _logger.LogInformation("[MLB] Single-date mode: syncing {Date}", _options.SyncDate.Value);
            await RunSyncForDateAsync(_options.SyncDate.Value, stoppingToken);
            return;
        }

        if (_options.IsBackfill)
        {
            await RunBackfillAsync(stoppingToken);
            return;
        }

        await RunGapDetectionAsync(stoppingToken);
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
            _logger.LogInformation("[MLB] Next sync scheduled at {Next} ET", next.Value);
            await Task.Delay(delay, ct);
            if (ct.IsCancellationRequested) break;

            await RunSyncForDateAsync(DateOnly.FromDateTime(DateTime.UtcNow), ct);
        }
    }

    private async Task RunGapDetectionAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NbaTrackerDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // MLB season starts late March — don't backfill before that
        var seasonStart = new DateOnly(today.Year, 3, 20);

        var lastSyncedDate = await db.SyncRuns
            .Where(r => r.Sport == "MLB"
                     && r.SyncDate != null
                     && (r.Status == SyncRunStatus.Success || r.Status == SyncRunStatus.Partial))
            .OrderByDescending(r => r.SyncDate)
            .Select(r => r.SyncDate)
            .FirstOrDefaultAsync(ct);

        if (lastSyncedDate is null)
        {
            _logger.LogInformation("[MLB] No previous sync found — skipping gap backfill");
        }
        else
        {
            for (var d = lastSyncedDate.Value.AddDays(1); d < today; d = d.AddDays(1))
            {
                if (d < seasonStart) continue;
                _logger.LogInformation("[MLB] Gap detected: syncing {Date}", d);
                await RunSyncForDateAsync(d, ct);
                if (ct.IsCancellationRequested) return;
            }
        }

        var todayDone = await db.SyncRuns
            .AnyAsync(r => r.Sport == "MLB"
                        && r.SyncDate == today
                        && (r.Status == SyncRunStatus.Success || r.Status == SyncRunStatus.Partial), ct);

        if (!todayDone)
        {
            _logger.LogInformation("[MLB] Today ({Date}) not yet synced — running on startup", today);
            await RunSyncForDateAsync(today, ct);
        }
        else
        {
            _logger.LogInformation("[MLB] Today ({Date}) already synced — skipping", today);
        }
    }

    private async Task RunBackfillAsync(CancellationToken ct)
    {
        // 2026 MLB season started March 27
        var start = new DateOnly(2026, 3, 27);
        var end = DateOnly.FromDateTime(DateTime.UtcNow);

        _logger.LogInformation("[MLB] Backfill mode: {Start} to {End}", start, end);

        for (var d = start; d <= end; d = d.AddDays(1))
        {
            await RunSyncForDateAsync(d, ct);
            if (ct.IsCancellationRequested) break;
        }

        _logger.LogInformation("[MLB] Backfill complete");
    }

    private async Task RunSyncForDateAsync(DateOnly date, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<MlbSyncOrchestrator>();
        await orchestrator.RunDailySyncAsync(date, ct);
    }
}