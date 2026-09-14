using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using NbaTracker.Data;
using NbaTracker.Worker;
using NbaTracker.Worker.Services;
using Polly;

// Load .env if present (local dev outside Docker). In Docker, real env vars
// already exist and take precedence — Load is a no-op when the file is absent.
Env.TraversePath().Load();

var builder = Host.CreateApplicationBuilder(args);

// Parse backfill flag: --backfill arg or BACKFILL=true env var
bool isBackfill = args.Contains("--backfill") ||
    builder.Configuration["BACKFILL"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

// Parse single-date override: SYNC_DATE=yyyy-MM-dd env var
DateOnly? syncDate = null;
var syncDateStr = builder.Configuration["SYNC_DATE"];
if (!string.IsNullOrEmpty(syncDateStr) && DateOnly.TryParse(syncDateStr, out var parsedSyncDate))
    syncDate = parsedSyncDate;

// One-time NFL historical-season backfill: NFL_HISTORICAL_SEASON=2025 env var
int? nflHistoricalSeason = null;
var nflHistoricalSeasonStr = builder.Configuration["NFL_HISTORICAL_SEASON"];
if (!string.IsNullOrEmpty(nflHistoricalSeasonStr) && int.TryParse(nflHistoricalSeasonStr, out var parsedSeason))
    nflHistoricalSeason = parsedSeason;

builder.Services.AddSingleton(new SyncOptions
{
    IsBackfill = isBackfill,
    SyncDate = syncDate,
    NflHistoricalSeason = nflHistoricalSeason
});

// SyncFileLogger is singleton — writes per-date and failed-days log files
builder.Services.AddSingleton<SyncFileLogger>();

// NBA orchestrator
builder.Services.AddScoped<SyncOrchestrator>();

// MLB orchestrator
builder.Services.AddScoped<MlbSyncOrchestrator>();

// NFL orchestrator
builder.Services.AddScoped<NflSyncOrchestrator>();

// DbContext
builder.Services.AddDbContext<NbaTrackerDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        x => x.MigrationsAssembly("NbaTracker.Data")));

// BallDontLie — NBA schedule + scores
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

// MLB Stats API — free, no key required
builder.Services.AddHttpClient<MlbStatsClient>(client =>
{
    client.BaseAddress = new Uri("https://statsapi.mlb.com/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddResilienceHandler("MlbRetry", pipeline =>
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

// Highlightly — NFL schedule + scores
builder.Services.AddHttpClient<NflStatsClient>(client =>
{
    client.BaseAddress = new Uri("https://american-football.highlightly.net/");
    client.DefaultRequestHeaders.Add("x-rapidapi-key", builder.Configuration["Highlightly:ApiKey"]!);
})
.AddResilienceHandler("HighlightlyRetry", pipeline =>
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

// The Odds API — shared by NBA and MLB, sport key passed per call
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

// Hosted services — all workers run concurrently
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<MlbWorker>();
builder.Services.AddHostedService<NflWorker>();

var host = builder.Build();
host.Run();