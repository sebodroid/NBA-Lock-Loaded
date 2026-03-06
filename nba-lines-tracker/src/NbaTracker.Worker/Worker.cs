using Cronos;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using NbaTracker.Data;
using NbaTracker.Data.Entities;
using NbaTracker.Worker.Services;

namespace NbaTracker.Worker;

public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SyncOptions _options;
    private readonly ILogger<Worker> _logger;

    private static readonly CronExpression DailySchedule =
        CronExpression.Parse("0 5 * * *", CronFormat.Standard);

    private static readonly TimeZoneInfo EasternTime =
        TimeZoneInfo.FindSystemTimeZoneById(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Eastern Standard Time"
                : "America/New_York");

    public Worker(
        IServiceScopeFactory scopeFactory,
        SyncOptions options,
        ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.SyncDate.HasValue)
        {
            _logger.LogInformation("Single-date mode: syncing {Date}", _options.SyncDate.Value);
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
            _logger.LogInformation("Next sync scheduled at {Next} ET", next.Value);
            await Task.Delay(delay, ct);
            if (ct.IsCancellationRequested) break;

            // Cron fires at 5 AM ET — run today's sync (startup already handles the idempotency
            // check, so if for any reason it was already done, we run it again intentionally here
            // to pick up final scores that weren't available at startup)
            await RunSyncForDateAsync(DateOnly.FromDateTime(DateTime.UtcNow), ct);
        }
    }

    private async Task RunGapDetectionAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NbaTrackerDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Find the most recent date we have a completed sync for
        var lastSyncedDate = await db.SyncRuns
            .Where(r => r.SyncDate != null
                     && (r.Status == SyncRunStatus.Success || r.Status == SyncRunStatus.Partial))
            .OrderByDescending(r => r.SyncDate)
            .Select(r => r.SyncDate)
            .FirstOrDefaultAsync(ct);

        if (lastSyncedDate is null)
        {
            _logger.LogInformation("No previous sync found — skipping gap backfill");
        }
        else
        {
            // Fill any calendar gaps between last synced date and today (exclusive)
            for (var d = lastSyncedDate.Value.AddDays(1); d < today; d = d.AddDays(1))
            {
                _logger.LogInformation("Gap detected: syncing {Date}", d);
                await RunSyncForDateAsync(d, ct);
                if (ct.IsCancellationRequested) return;
            }
        }

        // Always sync today on startup unless a successful run already exists for today
        var todayDone = await db.SyncRuns
            .AnyAsync(r => r.SyncDate == today
                        && (r.Status == SyncRunStatus.Success || r.Status == SyncRunStatus.Partial), ct);

        if (!todayDone)
        {
            _logger.LogInformation("Today ({Date}) not yet synced — running on startup", today);
            await RunSyncForDateAsync(today, ct);
        }
        else
        {
            _logger.LogInformation("Today ({Date}) already synced — skipping startup sync", today);
        }
    }

    private async Task RunBackfillAsync(CancellationToken ct)
    {
        // 2025-10-22 = first game of the 2025-26 NBA season
        var start = new DateOnly(2025, 10, 22);
        var end = DateOnly.FromDateTime(DateTime.UtcNow);

        _logger.LogInformation("Backfill mode: syncing {Start} to {End}", start, end);

        for (var d = start; d <= end; d = d.AddDays(1))
        {
            await RunSyncForDateAsync(d, ct);
            if (ct.IsCancellationRequested) break;
        }

        _logger.LogInformation("Backfill complete");
    }

    private async Task RunSyncForDateAsync(DateOnly date, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<SyncOrchestrator>();
        await orchestrator.RunDailySyncAsync(date, ct);
    }
}
