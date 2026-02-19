# Phase 2: Ingestion Worker - Research

**Researched:** 2026-02-18
**Domain:** .NET Worker Service, BallDontLie API, The Odds API, EF Core upsert patterns, daily scheduling
**Confidence:** HIGH (API schemas verified from official docs; .NET patterns verified from Microsoft Learn)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **Season scope:** Target 2025-26 (current). Continue future seasons automatically — no hardcoded season boundary. No 2024-25 backfill.
- **Sync schedule:** One combined daily job at 5am ET. Scores and lines fetched together.
- **Gap detection on startup:** If last successful sync >1 day old, backfill missed days before resuming normal schedule.
- **Initial season load:** Manually triggered via CLI arg or env flag (`BACKFILL=true` or `dotnet run -- --backfill`). Same code path as daily sync with a date range override. Loads all 2025-26 games from October 2025 to present.
- **API failures:** Retry 3x with backoff, then continue with whatever data was successfully fetched. Do NOT abort the whole run.
- **Every failure** logged to sync_runs with full error details.
- **Partial runs** continue and complete; missing data noted, not silently dropped.
- **Unresolved games:** If betting lines missing, store game record with null ATS/O/U fields, mark game as unresolved. Re-attempted on next sync run.
- **SyncRunStatus enum:** `SUCCESS` / `PARTIAL` / `FAILURE` (three-value).
  - SUCCESS: all games and lines fetched and processed cleanly
  - PARTIAL: some data fetched but errors occurred
  - FAILURE: sync could not complete in any meaningful way
- **Canonical sportsbook:** FanDuel primary (`fanduel`), HardRock fallback (`hardrockbet`). Configured in worker, not hardcoded.
- **Canonical matching key:** `{season}_{home_abbr}_{away_abbr}_{game_date_utc}`

### Claude's Discretion

- Exact retry backoff strategy (exponential vs fixed delay)
- Database transaction scope per sync run
- How gap detection identifies missed days (compare sync_runs timestamps vs expected schedule)
- Rate limiting implementation between API calls

### Deferred Ideas (OUT OF SCOPE)

- Admin status endpoint `/api/admin/sync-status` — Phase 3 only. Phase 2 writes the right data to sync_runs.
- 2024-25 historical backfill
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| DATA-01 | System syncs NBA game schedules and final scores daily from BallDontLie API | BallDontLie `/v1/games` endpoint schema, `seasons[]` and `dates[]` filters, pagination pattern |
| DATA-02 | System syncs betting lines (spread, total) daily from The Odds API (FanDuel primary, HardRock fallback) | The Odds API `/v4/sports/basketball_nba/odds` and `/v4/sports/basketball_nba/scores` endpoints, bookmaker key selection |
| DATA-03 | System calculates and stores ATS result (COVER / LOSS / PUSH) for each completed game | ATS calculation formula, FavoriteTeamId FK usage, spread sign semantics |
| DATA-04 | System calculates and stores O/U result (OVER / UNDER / PUSH) for each completed game | WentToOvertime field for push detection, total comparison formula |
| DATA-05 | Admin can view sync status (last run time, success/failure, error details) | SyncRun entity already in schema; worker writes correctly-structured rows |
| DATA-06 | System supports one-time historical data load for current 2025-26 season from game 1 | Backfill flag pattern, date range override through same sync code path |
</phase_requirements>

---

## Summary

Phase 2 implements a .NET Worker Service that pulls NBA game data from BallDontLie and betting lines from The Odds API, then calculates ATS/O/U results and writes them to PostgreSQL. The Phase 1 schema is already in place — all entity classes, DbContext, and the InitialCreate migration exist. The Worker project exists with an empty BackgroundService shell. Phase 2 fills that shell with real logic.

The two external APIs have asymmetric rate limits: BallDontLie free tier is 5 req/min while paid ALL-STAR is 60 req/min. The Odds API bills per request and limits historical scores to 3 days back via the scores endpoint. Cross-API matching is the most fragile part of the implementation because the two APIs use completely different game identifiers — the canonical matching key (`{season}_{home_abbr}_{away_abbr}_{game_date_utc}`) must be computed from both API responses and stored in `Game.OddsApiGameId`.

Scheduling with exact timezone-aware daily execution (5am ET) requires the Cronos library on top of .NET's native BackgroundService/PeriodicTimer. Microsoft.Extensions.Http.Resilience (part of .NET 9 ecosystem) provides first-party retry-with-backoff via `AddStandardResilienceHandler` or the customizable `AddResilienceHandler`, replacing the older Polly-only approach.

**Primary recommendation:** Use typed HttpClient classes registered via `AddHttpClient<T>` with `AddResilienceHandler` for 3-retry exponential backoff. Use Cronos for the 5am ET daily trigger. Use a single EF Core transaction per game (not per full sync run) to ensure partial progress is preserved on failure.

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.Extensions.Hosting` | 9.* | BackgroundService base, DI, config, IHostedService | Already in Worker.csproj; the canonical .NET Worker pattern |
| `Microsoft.EntityFrameworkCore` | 9.* | Data access, upsert via ExecuteUpdate | Already in project; version-matched to .NET 9 |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 9.* | PostgreSQL driver | Already in project |
| `Cronos` | 0.9.x | Cron expression parsing with timezone support (DST-safe) | HangfireIO project; the standard lightweight .NET cron lib when you don't need a full scheduler |
| `Microsoft.Extensions.Http.Resilience` | 9.* | Typed HttpClient retry / circuit-breaker / timeout | First-party Microsoft lib; replaces Polly-only HttpClientFactory pattern for .NET 8+ |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Text.Json` | built-in | Deserializing API responses | Already available in .NET 9; no third-party JSON lib needed |
| `Microsoft.Extensions.Configuration` | built-in | API keys from env vars / appsettings | Needed for `BALLDONTLIE_API_KEY`, `ODDS_API_KEY`, `CANONICAL_BOOKMAKER` config |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Cronos | Quartz.NET | Quartz is a full job scheduler with persistence — overkill for one daily job. Cronos is just cron expression parsing, which is all we need. |
| Cronos | Manual `TimeSpan.FromHours(24)` PeriodicTimer | PeriodicTimer doesn't know about time-of-day or DST; would drift by seconds each day and fire at wrong time after DST change |
| `AddResilienceHandler` | `AddPolicyHandler` (Polly v7) | The Polly v7 extension is the old pattern; `Microsoft.Extensions.Http.Resilience` is the .NET 8/9 first-party replacement that wraps Polly v8 |
| Per-game EF transactions | Single transaction for whole sync run | Single transaction means all-or-nothing: one API timeout aborts the entire run. Per-game or per-batch transactions let partial progress commit. |

### Installation

```bash
dotnet add package Cronos
dotnet add package Microsoft.Extensions.Http.Resilience
```

---

## Architecture Patterns

### Recommended Project Structure

```
NbaTracker.Worker/
├── Program.cs                          # DI registration, host config, backfill flag parsing
├── Worker.cs                           # BackgroundService shell — schedule loop, gap detection
├── Services/
│   ├── SyncOrchestrator.cs             # Coordinates the full daily sync: BDL → OddsAPI → calc → persist
│   ├── BallDontLieClient.cs            # Typed HttpClient — fetches games/teams from BallDontLie
│   ├── OddsApiClient.cs                # Typed HttpClient — fetches odds and scores from The Odds API
│   ├── GameMatchingService.cs          # Cross-API matching: BDL game ↔ OddsAPI event by canonical key
│   └── AtsOuCalculator.cs              # Pure calculation logic: given scores + line → ATS/OU result
└── Models/
    ├── BallDontLie/                    # Response DTOs for BallDontLie
    │   ├── BdlGame.cs
    │   ├── BdlTeam.cs
    │   └── BdlPagedResponse.cs
    └── OddsApi/                        # Response DTOs for The Odds API
        ├── OddsApiEvent.cs
        ├── OddsApiScore.cs
        └── OddsApiBookmaker.cs
```

### Pattern 1: Cronos-Based Daily Schedule (5am ET)

The Worker's ExecuteAsync loop calculates the next 5am ET occurrence via Cronos, delays until that instant, then calls the sync orchestrator.

```csharp
// Source: https://github.com/HangfireIO/Cronos
private static readonly CronExpression DailySchedule =
    CronExpression.Parse("0 5 * * *", CronFormat.Standard);

private static readonly TimeZoneInfo EasternTime =
    TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); // Windows
    // OR "America/New_York" on Linux (Docker)

protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // On startup: gap detection before entering the normal schedule loop
    await RunGapDetectionAsync(stoppingToken);

    while (!stoppingToken.IsCancellationRequested)
    {
        var now = DateTimeOffset.UtcNow;
        var next = DailySchedule.GetNextOccurrence(now, EasternTime);
        if (next is null) break;

        var delay = next.Value - now;
        await Task.Delay(delay, stoppingToken);

        await _syncOrchestrator.RunDailySyncAsync(DateOnly.FromDateTime(DateTime.UtcNow), stoppingToken);
    }
}
```

**IMPORTANT — Linux vs Windows timezone IDs:** The Worker runs in a Docker container on `dotnet/runtime:9.0` (Linux). Linux uses IANA timezone names (`"America/New_York"`), not Windows names (`"Eastern Standard Time"`). Use `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` to branch, or pin to IANA names and install `tzdata` in the Dockerfile. Alternatively, use the `TimeZoneConverter` NuGet package which handles both.

### Pattern 2: Typed HttpClient with Resilience

```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience
// In Program.cs
builder.Services.AddHttpClient<BallDontLieClient>(client =>
{
    client.BaseAddress = new Uri("https://api.balldontlie.io/nba/v1/");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue(builder.Configuration["BallDontLie:ApiKey"]!);
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

builder.Services.AddHttpClient<OddsApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.the-odds-api.com/v4/");
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
```

### Pattern 3: BallDontLie Pagination

BallDontLie uses cursor-based pagination, not offset/limit. You must follow the cursor until `meta.next_cursor` is null.

```csharp
// Response shape
public class BdlPagedResponse<T>
{
    public List<T> Data { get; set; } = [];
    public BdlMeta Meta { get; set; } = null!;
}
public class BdlMeta
{
    public int? NextCursor { get; set; }
    public int PerPage { get; set; }
}

// Pagination loop
var allGames = new List<BdlGame>();
int? cursor = null;
do
{
    var url = $"games?seasons[]=2025&per_page=100{(cursor.HasValue ? $"&cursor={cursor}" : "")}";
    var page = await _http.GetFromJsonAsync<BdlPagedResponse<BdlGame>>(url, ct);
    allGames.AddRange(page!.Data);
    cursor = page.Meta.NextCursor;
} while (cursor.HasValue);
```

### Pattern 4: ATS/O/U Calculation

The calculation is pure math — no external calls. The tricky parts:
- ATS is from the **favorite's** perspective. The `GameLine.FavoriteTeamId` FK tells us who the favorite is (never infer from spread sign alone — per Phase 1 decision).
- Spread stored as absolute value.
- Push detection requires exact decimal comparison.
- O/U push can happen when final total hits the line **without** overtime involvement, OR when WentToOvertime is true and total lands exactly on the line.

```csharp
// Pure static calculation — no EF, no HTTP
public static AtsResult CalculateAts(
    int homeScore, int awayScore,
    decimal spread, int favoriteTeamId,
    int homeTeamId)
{
    // margin = favorite's score - underdog's score
    bool homeIsFavorite = favoriteTeamId == homeTeamId;
    int favoriteScore = homeIsFavorite ? homeScore : awayScore;
    int underdogScore = homeIsFavorite ? awayScore : homeScore;
    decimal margin = favoriteScore - underdogScore;

    if (margin > spread) return AtsResult.Cover;
    if (margin < spread) return AtsResult.Loss;
    return AtsResult.Push;
}

public static OuResult CalculateOu(int homeScore, int awayScore, decimal total)
{
    decimal combinedScore = homeScore + awayScore;
    if (combinedScore > total) return OuResult.Over;
    if (combinedScore < total) return OuResult.Under;
    return OuResult.Push;
}
```

**Note:** `AtsResult.Cover` means the favorite covered. The home team's `HomeAtsResult` is Cover if the home team was the favorite and they covered, or Cover if the home team was the underdog and the favorite failed to cover. Resolve both sides:

```csharp
// HomeAtsResult from the home team's perspective
var favoriteResult = CalculateAts(homeScore, awayScore, spread, favoriteTeamId, homeTeamId);
gameResult.HomeAtsResult = homeTeamId == favoriteTeamId ? favoriteResult
    : (favoriteResult == AtsResult.Cover ? AtsResult.Loss
       : favoriteResult == AtsResult.Loss ? AtsResult.Cover
       : AtsResult.Push);
gameResult.AwayAtsResult = awayTeamId == favoriteTeamId ? favoriteResult
    : (favoriteResult == AtsResult.Cover ? AtsResult.Loss
       : favoriteResult == AtsResult.Loss ? AtsResult.Cover
       : AtsResult.Push);
```

### Pattern 5: Gap Detection on Startup

```csharp
private async Task RunGapDetectionAsync(CancellationToken ct)
{
    // Find the most recent successful or partial sync
    var lastSync = await _db.SyncRuns
        .Where(r => r.Status == SyncRunStatus.Success || r.Status == SyncRunStatus.Partial)
        .OrderByDescending(r => r.CompletedAt)
        .FirstOrDefaultAsync(ct);

    if (lastSync?.CompletedAt is null) return; // First run — backfill handles it

    var expectedNextSync = lastSync.CompletedAt.Value.Date.AddDays(1);
    var today = DateTime.UtcNow.Date;

    // If we missed days, backfill from the day after last sync to yesterday
    for (var date = DateOnly.FromDateTime(expectedNextSync);
         date < DateOnly.FromDateTime(today);
         date = date.AddDays(1))
    {
        await _syncOrchestrator.RunDailySyncAsync(date, ct);
    }
}
```

### Pattern 6: Upsert Pattern for EF Core / PostgreSQL

Games already in the DB must be updated (scores finalized, status changed). New games must be inserted. EF Core 9 does not have a native PostgreSQL ON CONFLICT DO UPDATE syntax built-in, but Npgsql 9 supports it via raw SQL or through ExecuteUpdate.

**Recommended approach:** Query by `NbaGameId` (unique index exists), then update-or-insert in code:

```csharp
var existing = await _db.Games
    .FirstOrDefaultAsync(g => g.NbaGameId == bdlGame.Id.ToString(), ct);

if (existing is null)
{
    _db.Games.Add(MapToEntity(bdlGame, homeTeam, awayTeam));
}
else
{
    // Update only fields that can change after a game is created
    existing.Status = MapStatus(bdlGame.Status);
    existing.HomeScore = bdlGame.HomeTeamScore;
    existing.AwayScore = bdlGame.VisitorTeamScore;
    existing.WentToOvertime = bdlGame.Period > 4;
    existing.UpdatedAt = DateTime.UtcNow;
}
await _db.SaveChangesAsync(ct);
```

**Transaction scope (Claude's Discretion — recommended):** One `SaveChangesAsync` per game (or small batch). Do NOT wrap the entire sync run in one transaction. A single large transaction means a network error at game 1200/1230 rolls back all 1200 previously successful writes. Fine-grained transactions preserve partial progress and honor the "partial runs continue" requirement.

### Pattern 7: SyncRun Lifecycle

```csharp
// Open a sync run record at the start
var syncRun = new SyncRun
{
    StartedAt = DateTime.UtcNow,
    Status = SyncRunStatus.Running
};
_db.SyncRuns.Add(syncRun);
await _db.SaveChangesAsync(ct);

var errors = new List<string>();
int gamesProcessed = 0;

try
{
    // ... do all the work, collect errors into list ...

    syncRun.Status = errors.Count == 0 ? SyncRunStatus.Success : SyncRunStatus.Partial;
    syncRun.GamesProcessed = gamesProcessed;
    syncRun.ErrorDetails = errors.Count > 0 ? JsonSerializer.Serialize(errors) : null;
}
catch (Exception ex)
{
    syncRun.Status = SyncRunStatus.Failed;
    syncRun.ErrorDetails = JsonSerializer.Serialize(new { fatal = ex.Message });
}
finally
{
    syncRun.CompletedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync(CancellationToken.None); // don't use stoppingToken here
}
```

### Pattern 8: Backfill Flag Parsing

```csharp
// In Program.cs — before host.Build()
bool isBackfill = args.Contains("--backfill") ||
    builder.Configuration["BACKFILL"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

builder.Services.AddSingleton(new SyncOptions { IsBackfill = isBackfill });
```

```csharp
// In Worker.ExecuteAsync — skip the schedule loop, run a date range
if (_options.IsBackfill)
{
    var start = new DateOnly(2025, 10, 22); // First game of 2025-26 season
    var end = DateOnly.FromDateTime(DateTime.UtcNow);
    for (var date = start; date <= end; date = date.AddDays(1))
        await _syncOrchestrator.RunDailySyncAsync(date, stoppingToken);
    return; // Worker exits after backfill
}
```

### Anti-Patterns to Avoid

- **Inferring favorite from spread sign:** The spread column stores an absolute value. Always use `GameLine.FavoriteTeamId` to know who the favorite is. Never assume negative spread = home favorite.
- **One DB transaction for the entire sync:** Violates the "partial runs continue" requirement. A crash at the end discards everything.
- **Hardcoding "fanduel" or "hardrockbet":** Both sportsbook keys must come from configuration so they can be changed without a code deploy.
- **DateTime.Now for scheduling:** Always use `DateTimeOffset.UtcNow` and let Cronos convert to ET. `DateTime.Now` inside a Linux Docker container returns UTC anyway (no local timezone), making all timezone calculations wrong.
- **Using `stoppingToken` in the final `SaveChangesAsync`:** If the host is shutting down, you still need to persist the SyncRun completion record. Pass `CancellationToken.None` for the final status write.
- **Not seeding Teams on first run:** BallDontLie games reference team IDs. Teams must be seeded before games are inserted or FK constraints will fail. Add a teams-seed step at the top of the sync orchestrator if the Teams table is empty.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Retry with exponential backoff | Custom retry loops | `Microsoft.Extensions.Http.Resilience` `AddResilienceHandler` | Handles jitter, respects 429/503/408, integrates with DI-scoped HttpClient |
| Cron schedule with DST safety | `TimeSpan.FromHours(24)` loop | Cronos | DST transitions cause 23h or 25h days; Cronos handles this correctly |
| HTTP connection lifecycle | `new HttpClient()` per request | `IHttpClientFactory` / typed clients | Socket exhaustion on long-running workers |
| Pagination across BDL pages | Manual index tracking | Cursor loop pattern | BDL uses cursor-based pagination, not page numbers |

**Key insight:** The hardest parts of this phase are not the calculations (those are straightforward decimal math) — they are cross-API game matching and the timezone-aware scheduler. Both have well-established library solutions; neither should be solved with custom code.

---

## Common Pitfalls

### Pitfall 1: BallDontLie Season Integer vs String

**What goes wrong:** The `season` parameter in BallDontLie API is an integer. The 2025-26 NBA season is `seasons[]=2025` (the starting year). However, the `Game.Season` column in our schema stores `"2025-26"` (string format). These must be mapped correctly.

**How to avoid:** In the BDL client, accept `int season = 2025`. In the mapper, convert to string: `$"{season}-{(season + 1) % 100:D2}"` → `"2025-26"`.

### Pitfall 2: BallDontLie "Final" Status Is a String, Not an Enum

**What goes wrong:** BallDontLie returns `status` as a string. For finished games it returns `"Final"`. For in-progress games it returns the period string like `"Q4"` or `"Halftime"`. For upcoming games it returns the tip-off time like `"7:30 pm ET"`. There is no simple boolean `completed` field.

**How to avoid:** Only process ATS/O/U when `status == "Final"`. Map to the internal `"FINAL"` / `"LIVE"` / `"SCHEDULED"` / `"POSTPONED"` values. A game with `postponed: true` in the response should be mapped to `"POSTPONED"`.

**Warning sign:** ATS being calculated for in-progress games with a partial score.

### Pitfall 3: Overtime Detection Field Mismatch

**What goes wrong:** `Game.WentToOvertime` is a `bool?` in the schema. BallDontLie returns `period` as an integer. Period > 4 means overtime occurred. If you don't set this field, O/U push detection is impossible when `homeScore + awayScore == total` and the game went to overtime.

**How to avoid:** Always set `WentToOvertime = bdlGame.Period > 4` when the game status is Final.

### Pitfall 4: The Odds API Rate Limit and Request Budget

**What goes wrong:** The Odds API bills per API request. Each call to `/odds` or `/scores` consumes quota. Fetching odds for every single game date individually would burn through quota quickly.

**How to avoid:** Fetch all games for the sport in one call per day (the `/v4/sports/basketball_nba/odds` endpoint returns all upcoming/live events in one response). For historical scores, use `/v4/sports/basketball_nba/scores?daysFrom=1` (max 3 days back). Do not loop per-game; loop per-date at most.

**Warning sign:** Quota exhausting within days of first run.

### Pitfall 5: Cross-API Game Matching Failure

**What goes wrong:** BallDontLie uses its own numeric game ID. The Odds API uses a UUID event ID. The canonical matching key `{season}_{home_abbr}_{away_abbr}_{game_date_utc}` must be constructed identically from both sides. If one API uses `"BOS"` and the other uses `"Boston Celtics"`, the match fails.

**How to avoid:**
- For BallDontLie: use `home_team.abbreviation` and `visitor_team.abbreviation` directly from the response.
- For The Odds API: `home_team` and `away_team` are the full team names (e.g., `"Boston Celtics"`). You need a lookup table mapping Odds API team names to BallDontLie abbreviations.
- `game_date_utc`: BallDontLie returns game date in Eastern time (e.g., `"2025-11-01"`). Games starting after midnight ET on the same calendar day are the same "game date" by NBA convention. Normalize to UTC midnight for the matching key.

**Recommendation (Claude's Discretion):** Pre-build a static 30-entry dictionary mapping The Odds API's full NBA team names to abbreviations. The NBA has 30 teams and they don't change. This is safer than trying to fuzzy-match or call a teams endpoint on the Odds API side.

### Pitfall 6: Docker Linux Timezone ID

**What goes wrong:** The Worker runs in a Linux container. `TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")` throws `TimeZoneNotFoundException` on Linux, which uses IANA zone IDs.

**How to avoid:** Two options:
1. Use `"America/New_York"` as the timezone ID on Linux. Add OS detection so the code works locally on Windows too:
   ```csharp
   private static readonly string EtZoneId =
       RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
           ? "Eastern Standard Time"
           : "America/New_York";
   ```
2. Add `tzdata` to the Worker Dockerfile:
   ```dockerfile
   RUN apt-get update && apt-get install -y tzdata && rm -rf /var/lib/apt/lists/*
   ```
   Then use IANA IDs. The `dotnet/runtime:9.0` base image does NOT include tzdata by default.

### Pitfall 7: EF Core Scoped DbContext in a Singleton BackgroundService

**What goes wrong:** `NbaTrackerDbContext` is registered as `Scoped`. `BackgroundService` is effectively a singleton. Injecting `NbaTrackerDbContext` directly into Worker causes a captive dependency error at startup.

**How to avoid:** Inject `IServiceScopeFactory` into the Worker and create a new scope per sync run:
```csharp
using var scope = _scopeFactory.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<NbaTrackerDbContext>();
```

### Pitfall 8: The Odds API Bookmaker Key for HardRock

**What goes wrong:** The exact bookmaker key string for Hard Rock Bet in The Odds API is not confirmed by official documentation in search results. Community sources suggest `"hardrockbet"` but this needs validation against the live `/v4/sports/basketball_nba/odds?regions=us&markets=spreads` response.

**How to avoid:** On first run against the live API, log all bookmaker keys returned for any basketball_nba event. The canonical keys are available via the `/v4/sports/basketball_nba/odds` response `bookmakers[].key` field. This must be verified before deploying.

---

## Code Examples

### BallDontLie Game DTO (verified response fields)

```csharp
// Source: https://nba.balldontlie.io/
public class BdlGame
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; } = null!;     // "2025-11-01" — Eastern time date

    [JsonPropertyName("season")]
    public int Season { get; set; }               // 2025 for 2025-26 season

    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;   // "Final", "Q4", "7:30 pm ET", etc.

    [JsonPropertyName("period")]
    public int Period { get; set; }               // 0=not started, 4=regulation, 5+=OT

    [JsonPropertyName("postseason")]
    public bool Postseason { get; set; }

    [JsonPropertyName("postponed")]
    public bool Postponed { get; set; }

    [JsonPropertyName("home_team_score")]
    public int? HomeTeamScore { get; set; }

    [JsonPropertyName("visitor_team_score")]
    public int? VisitorTeamScore { get; set; }

    [JsonPropertyName("home_team")]
    public BdlTeam HomeTeam { get; set; } = null!;

    [JsonPropertyName("visitor_team")]
    public BdlTeam VisitorTeam { get; set; } = null!;
}

public class BdlTeam
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; } = null!;  // "BOS", "LAL", etc.

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = null!;
}
```

### The Odds API Odds DTO (verified response fields)

```csharp
// Source: https://the-odds-api.com/liveapi/guides/v4/
public class OddsApiEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;           // UUID — OddsApiGameId stored in Game

    [JsonPropertyName("sport_key")]
    public string SportKey { get; set; } = null!;     // "basketball_nba"

    [JsonPropertyName("commence_time")]
    public DateTimeOffset CommenceTime { get; set; }  // UTC

    [JsonPropertyName("home_team")]
    public string HomeTeam { get; set; } = null!;     // "Boston Celtics" (full name)

    [JsonPropertyName("away_team")]
    public string AwayTeam { get; set; } = null!;

    [JsonPropertyName("bookmakers")]
    public List<OddsApiBookmaker> Bookmakers { get; set; } = [];
}

public class OddsApiBookmaker
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = null!;          // "fanduel", "hardrockbet"

    [JsonPropertyName("markets")]
    public List<OddsApiMarket> Markets { get; set; } = [];
}

public class OddsApiMarket
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = null!;          // "spreads", "totals"

    [JsonPropertyName("outcomes")]
    public List<OddsApiOutcome> Outcomes { get; set; } = [];
}

public class OddsApiOutcome
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;         // team name or "Over"/"Under"

    [JsonPropertyName("price")]
    public int Price { get; set; }                    // American odds: -110, +100

    [JsonPropertyName("point")]
    public decimal? Point { get; set; }               // Spread: -7.5 for favorite; Total: 220.5
}
```

### The Odds API — Extracting Canonical Line

```csharp
// Select FanDuel primary, HardRock fallback
public static OddsApiBookmaker? SelectCanonicalBookmaker(
    List<OddsApiBookmaker> bookmakers,
    string primaryKey,    // "fanduel" from config
    string fallbackKey)   // "hardrockbet" from config
{
    return bookmakers.FirstOrDefault(b => b.Key == primaryKey)
        ?? bookmakers.FirstOrDefault(b => b.Key == fallbackKey);
}

// From the spreads market — point is negative for the favorite
// e.g., Celtics outcome: name="Boston Celtics", point=-7.5 → Celtics are -7.5 favorite
// e.g., Lakers outcome:  name="Los Angeles Lakers", point=+7.5 → Lakers are +7.5 underdog
// The favorite has the negative point value
public static (decimal spread, string favoriteTeamName) ExtractSpread(OddsApiMarket spreadsMarket)
{
    var favorite = spreadsMarket.Outcomes.MinBy(o => o.Point ?? 0)!;
    return (Math.Abs(favorite.Point!.Value), favorite.Name);
}
```

### Canonical Matching Key Construction

```csharp
// From BallDontLie side:
public static string BuildCanonicalKey(int season, string homeAbbr, string awayAbbr, DateOnly gameDate)
    => $"{season}-{(season + 1) % 100:D2}_{homeAbbr}_{awayAbbr}_{gameDate:yyyy-MM-dd}";
// e.g. "2025-26_BOS_LAL_2025-11-01"

// From The Odds API side:
// homeTeam = "Boston Celtics" → look up abbreviation in static dictionary
// commenceTime is UTC → convert to ET date for the NBA "game date"
public static string BuildCanonicalKey(
    int season,
    string oddsApiHomeTeam, string oddsApiAwayTeam,
    DateTimeOffset commenceTimeUtc,
    TimeZoneInfo easternTime,
    Dictionary<string, string> teamNameToAbbr)
{
    var homeAbbr = teamNameToAbbr[oddsApiHomeTeam];
    var awayAbbr = teamNameToAbbr[oddsApiAwayTeam];
    var etDate = TimeZoneInfo.ConvertTime(commenceTimeUtc, easternTime);
    var gameDate = DateOnly.FromDateTime(etDate.DateTime);
    return BuildCanonicalKey(season, homeAbbr, awayAbbr, gameDate);
}
```

### Worker.cs Scoped DbContext Pattern

```csharp
public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SyncOptions _options;
    private readonly ILogger<Worker> _logger;

    public Worker(IServiceScopeFactory scopeFactory, SyncOptions options, ILogger<Worker> logger)
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
        // Cronos loop — see Pattern 1
    }

    private async Task<SyncOrchestrator> CreateOrchestrator()
    {
        var scope = _scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<SyncOrchestrator>();
        // Note: scope must be disposed after sync completes — use 'using var scope' in callers
    }
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `AddPolicyHandler` (Polly v7 extension) | `AddResilienceHandler` (Microsoft.Extensions.Http.Resilience, Polly v8) | .NET 8 (2023) | First-party support; Polly v7 extension NuGet still works but is community-maintained |
| `System.Threading.Timer` callback | `PeriodicTimer` + `await WaitForNextTickAsync` | .NET 6 (2021) | Async-safe, no overlapping, no fire-and-forget issues |
| Offset/page pagination | Cursor-based pagination | BDL API (2023) | Must follow cursor until null; `per_page` index is gone |
| BallDontLie v0 (free, no auth) | BallDontLie v1 (requires API key) | 2023 | ALL-STAR tier needed for >5 req/min; free tier only 5/min |

**Deprecated/outdated:**
- `BallDontLie v0` free tier with no auth key: Replaced by v1 requiring auth. Any code samples using `api.balldontlie.io/api/v1/games` without an auth header are outdated.
- `Microsoft.Extensions.Http.Polly`: Still works but superseded by `Microsoft.Extensions.Http.Resilience` for .NET 8+.

---

## Open Questions

1. **Exact The Odds API bookmaker key for HardRock**
   - What we know: The sportsbook is called "Hard Rock Bet" and the likely key is `"hardrockbet"` based on convention
   - What's unclear: Whether `"hardrockbet"` is the active key in The Odds API's NBA coverage as of Feb 2026, or if it has been delisted (the API occasionally removes bookmakers)
   - Recommendation: First task in 02-02 should be a one-off API call to log all available bookmaker keys for `basketball_nba`. Make the fallback key configurable so it can be updated without code changes.

2. **BallDontLie rate limit tier needed**
   - What we know: Free tier = 5 req/min; ALL-STAR = 60 req/min
   - What's unclear: How many paginated requests the initial backfill of the full 2025-26 season requires (roughly 1230 games ÷ 100 per page = ~13 pages). Free tier can do this in 3 minutes; should be fine.
   - Recommendation: Free tier is sufficient for the daily sync (a few requests/day). The backfill will work on free tier. If the user wants real-time scores, ALL-STAR is needed — but the context says daily sync only.

3. **The Odds API historical lines availability**
   - What we know: The scores endpoint returns completed games up to 3 days in the past via `daysFrom`. Historical pre-game line data may not be available through the standard `/odds` endpoint for past events.
   - What's unclear: For the backfill of October–January games, will The Odds API return historical pre-game spread lines? If not, those games will have null ATS/O/U and remain "unresolved" permanently.
   - Recommendation: Investigate The Odds API historical odds endpoint (if any) in plan 02-02. The user accepted that games can have null ATS/O/U if lines are unavailable — this is an acceptable outcome per the decisions doc.

4. **DbContext scope disposal in Worker**
   - What we know: BackgroundService is a singleton; DbContext is scoped; must use IServiceScopeFactory
   - What's unclear: Whether SyncOrchestrator and its dependencies should all be registered as Scoped (resolved per sync run scope) vs Transient
   - Recommendation: Register SyncOrchestrator, BallDontLieClient, and OddsApiClient as Scoped. Typed HttpClient clients are already managed by IHttpClientFactory's internal pooling — registering them as Scoped is safe.

---

## Sources

### Primary (HIGH confidence)

- `https://nba.balldontlie.io/` — Official BallDontLie API docs: endpoints, authentication, pagination, rate limits, response schemas, period field for overtime detection
- `https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience` — Microsoft official docs for `Microsoft.Extensions.Http.Resilience`, `AddResilienceHandler`, retry options with exponential backoff
- `https://github.com/HangfireIO/Cronos` — Official Cronos repo: cron parsing, `GetNextOccurrence` with `TimeZoneInfo`, DST handling
- `https://the-odds-api.com/liveapi/guides/v4/` — The Odds API v4 documentation: `/scores`, `/odds` endpoints, `daysFrom` parameter, bookmaker key schema, `commence_time` UTC format
- Phase 1 codebase: entity classes, DbContext, Worker.csproj, docker-compose.yml — examined directly from `C:/Users/barne/Coding/Claude/nba-lines-tracker/src/`

### Secondary (MEDIUM confidence)

- WebSearch results on BallDontLie `season` integer format, `status` string values, `period` field for overtime — corroborated by official docs page structure
- WebSearch results on Cronos for .NET BackgroundService daily scheduling patterns — corroborated by official Cronos GitHub

### Tertiary (LOW confidence)

- HardRock bookmaker key `"hardrockbet"` in The Odds API — inferred from naming convention and community sources; not confirmed by official The Odds API bookmaker list page. Must validate against live API response before coding the fallback lookup.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — verified from official Microsoft and library docs
- Architecture patterns: HIGH — based on official docs and existing Phase 1 code
- BallDontLie API schema: HIGH — verified from official docs at nba.balldontlie.io
- The Odds API schema: MEDIUM-HIGH — verified from official v4 docs; bookmaker key for HardRock is LOW
- ATS/O/U calculation: HIGH — deterministic math, no external dependency
- Pitfalls: HIGH — Linux timezone and scoped DbContext pitfalls are well-documented; HardRock key is LOW

**Research date:** 2026-02-18
**Valid until:** 2026-03-18 (API schemas are stable; bookmaker availability can change monthly)
