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

    private const int RollingWindowDays = 7;

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

        var seasonYear = today.Month <= 2 ? today.Year - 1 : today.Year;
        if (!SportSeasonBoundaries.CfbRegularSeasonStarts.TryGetValue(seasonYear, out var seasonStart))
        {
            _logger.LogInformation("[CFB] No season start on record for {Year} — skipping gap backfill", seasonYear);
            return;
        }

        var windowStart = today.AddDays(-(RollingWindowDays - 1));

        var runs = await db.SyncRuns
            .Where(r => r.Sport == "CFB"
                     && r.SyncDate != null
                     && (r.Status == SyncRunStatus.Success || r.Status == SyncRunStatus.Partial))
            .Select(r => new { SyncDate = r.SyncDate!.Value, r.StartedAt })
            .ToListAsync(ct);

        var missing = FindDatesToBackfill(
            runs.Select(r => (r.SyncDate, r.StartedAt)), seasonStart, windowStart);

        if (missing.Count == 0) return;

        _logger.LogInformation(
            "[CFB] {Count} season dates have no completed sync ({First} to {Last}) — backfilling",
            missing.Count, missing[0], missing[^1]);

        foreach (var d in missing)
        {
            await RunSyncForDateAsync(d, ct);
            if (ct.IsCancellationRequested) return;
        }
    }

    public static List<DateOnly> FindDatesToBackfill(
        IEnumerable<(DateOnly SyncDate, DateTime StartedAt)> completedRuns,
        DateOnly seasonStart,
        DateOnly windowStart)
    {
        var covered = completedRuns
            .Where(r => DateOnly.FromDateTime(r.StartedAt) > r.SyncDate)
            .Select(r => r.SyncDate)
            .ToHashSet();

        var missing = new List<DateOnly>();
        for (var d = seasonStart; d < windowStart; d = d.AddDays(1))
        {
            if (!covered.Contains(d)) missing.Add(d);
        }
        return missing;
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
