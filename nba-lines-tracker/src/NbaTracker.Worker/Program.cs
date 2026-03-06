using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using NbaTracker.Data;
using NbaTracker.Worker;
using NbaTracker.Worker.Services;
using Polly;

var builder = Host.CreateApplicationBuilder(args);

// Parse backfill flag: --backfill arg or BACKFILL=true env var
bool isBackfill = args.Contains("--backfill") ||
    builder.Configuration["BACKFILL"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

// Parse single-date override: SYNC_DATE=yyyy-MM-dd env var
DateOnly? syncDate = null;
var syncDateStr = builder.Configuration["SYNC_DATE"];
if (!string.IsNullOrEmpty(syncDateStr) && DateOnly.TryParse(syncDateStr, out var parsedSyncDate))
    syncDate = parsedSyncDate;

builder.Services.AddSingleton(new SyncOptions { IsBackfill = isBackfill, SyncDate = syncDate });

// SyncFileLogger is singleton — writes per-date and failed-days log files
builder.Services.AddSingleton<SyncFileLogger>();

// Register SyncOrchestrator as Scoped (resolved per sync run scope in Worker)
builder.Services.AddScoped<SyncOrchestrator>();

// Register DbContext to validate the project reference compiles and connects
builder.Services.AddDbContext<NbaTrackerDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        x => x.MigrationsAssembly("NbaTracker.Data")));

// BallDontLie typed HttpClient with 3-retry exponential backoff
builder.Services.AddHttpClient<BallDontLieClient>(client =>
{
    client.BaseAddress = new Uri("https://api.balldontlie.io/nba/v1/");
    client.DefaultRequestHeaders.Add("Authorization", builder.Configuration["BallDontLie:ApiKey"]!);
})
.AddResilienceHandler("BdlRetry", pipeline =>
{
    pipeline.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromSeconds(2)
    });
    pipeline.AddTimeout(TimeSpan.FromSeconds(30));
});

// The Odds API client — NBA betting lines (spreads, totals) from FanDuel / HardRock
builder.Services.AddHttpClient<OddsApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.the-odds-api.com/");
})
.AddResilienceHandler("OddsApiRetry", pipeline =>
{
    pipeline.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromSeconds(2)
    });
    pipeline.AddTimeout(TimeSpan.FromSeconds(30));
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
