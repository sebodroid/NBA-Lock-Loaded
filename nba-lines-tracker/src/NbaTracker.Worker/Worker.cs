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

            await RunSyncForDateAsync(DateOnly.FromDateTime(DateTime.UtcNow), ct);
        }
    }

    private async Task RunGapDetectionAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NbaTrackerDbContext>();

        var lastSync = await db.SyncRuns
            .Where(r => r.Status == SyncRunStatus.Success || r.Status == SyncRunStatus.Partial)
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync(ct);

        if (lastSync?.CompletedAt is null)
        {
            _logger.LogInformation("No previous sync found — skipping gap detection");
            return;
        }

        // Note: gap detection may re-run a date that already has a PARTIAL sync_run.
        // This is intentional — the upsert logic is idempotent, so duplicate runs for the same date are safe.

        var expectedNextSync = lastSync.CompletedAt.Value.Date.AddDays(1);
        var today = DateTime.UtcNow.Date;

        for (var d = DateOnly.FromDateTime(expectedNextSync); d < DateOnly.FromDateTime(today); d = d.AddDays(1))
        {
            _logger.LogInformation("Gap detected: backfilling {Date}", d);
            await RunSyncForDateAsync(d, ct);
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
