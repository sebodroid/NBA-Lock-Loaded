---
phase: 02-ingestion-worker
plan: 04
subsystem: infra
tags: [cronos, backgroundservice, sync-orchestrator, backfill, docker, dotnet]
requires:
  - phase: 02-01
    provides: BallDontLieClient
  - phase: 02-02
    provides: OddsApiClient with ExtractSpread/ExtractTotal/SelectCanonicalBookmaker
  - phase: 02-03
    provides: AtsOuCalculator, GameMatchingService
  - phase: 01-data-foundation
    provides: NbaTrackerDbContext, SyncRun, Game, GameLine, GameResult entities
provides:
  - SyncOrchestrator: full BDL->Odds->match->upsert->ATS/OU pipeline with SyncRun lifecycle
  - Worker: Cronos 5am ET DST-safe schedule, startup gap detection, backfill mode
  - SyncOptions: IsBackfill flag
  - Dockerfile (Worker): tzdata for IANA timezone in Linux container
affects: [03-api-layer]
tech-stack:
  added: [Cronos]
  patterns: [scoped-service-from-singleton, sync-run-lifecycle, partial-run-resilience]
key-files:
  created:
    - nba-lines-tracker/src/NbaTracker.Worker/Services/SyncOrchestrator.cs
    - nba-lines-tracker/src/NbaTracker.Worker/SyncOptions.cs
  modified:
    - nba-lines-tracker/src/NbaTracker.Worker/Worker.cs
    - nba-lines-tracker/src/NbaTracker.Worker/Program.cs
    - nba-lines-tracker/src/NbaTracker.Worker/Dockerfile
key-decisions:
  - "SaveChangesAsync(CancellationToken.None) in finally — SyncRun status always persisted even on cancellation"
  - "Per-game SaveChangesAsync — no giant transaction, so partial success is safe"
  - "IServiceScopeFactory per sync run — scoped DbContext resolved safely from singleton BackgroundService"
  - "Season from date: month >= 10 => year, else year - 1 (handles Jan-Sep cross-year games)"
  - "Dockerfile at src/NbaTracker.Worker/Dockerfile (not nba-lines-tracker/Dockerfile.worker) — plan named incorrectly, actual location used"
  - "WentToOvertime set to true when Period > 4, preserved as existing value when not yet final"
requirements-completed: [DATA-05, DATA-06]
duration: 25min
completed: 2026-02-19
---

# Phase 02-04: SyncOrchestrator and Cronos Scheduler Summary

**One-liner:** Full BDL-to-Odds-API sync pipeline with per-game resilience, SyncRun observability, and Cronos 5am ET DST-safe scheduling with startup gap detection and backfill mode.

## What Was Built

### SyncOrchestrator

The core sync pipeline (`Services/SyncOrchestrator.cs`) orchestrates the complete daily data ingestion:

1. **SyncRun lifecycle:** Creates a `SyncRun { Status=Running }` record at start; `finally` block always sets `CompletedAt` and saves with `CancellationToken.None` — never the stoppingToken — so the record is persisted even if the host is stopping.

2. **Team seed:** On first run (Teams table empty), calls `BallDontLieClient.GetTeamsAsync`, upserts all 30 teams into the DB.

3. **BDL game upsert:** Season derived from date (`month >= 10 ? year : year - 1`). Calls `BallDontLieClient.GetGamesAsync(season, date)`. For each game, maps BDL status to `FINAL/POSTPONED/LIVE/SCHEDULED`, upserts `Game` entity (create or update). Each game gets its own `SaveChangesAsync(ct)` inside a per-game try/catch.

4. **Odds API line matching:** Single `GetOddsAsync()` call fetches all current NBA lines. Builds a `Dictionary<string, OddsApiEvent>` keyed by canonical key (`GameMatchingService.BuildCanonicalKeyFromOddsApi`). For each DB game on the date, looks up the Odds API event by the BDL canonical key.

5. **Line extraction:** `OddsApiClient.SelectCanonicalBookmaker` picks FanDuel (primary) or HardRock (fallback). `ExtractSpread` returns absolute spread, favorite team name (Odds API full name), and both sides' American odds. `TeamNameToAbbreviation` converts the full name to abbreviation, then a DB lookup resolves `FavoriteTeamId`.

6. **GameLine upsert:** Creates or updates the `GameLine` entity with `Spread`, `FavoriteTeamId`, `Total`, `HomeSpreadOdds`, `AwaySpreadOdds`, `OverOdds`, `UnderOdds`, `Bookmaker`, `LineTimestamp`, `UpdatedAt`.

7. **OddsApiGameId storage:** Sets `game.OddsApiGameId = oddsEvent.Id` for future re-matching.

8. **ATS/OU calculation:** For `Status == "FINAL"` games with non-null spread, total, and scores: `AtsOuCalculator.CalculateFavoriteAts` → `DeriveBothSides` → `AtsOuCalculator.CalculateOu` → upsert `GameResult`.

9. **Status reporting:** `Success` when zero errors, `Partial` when some games had errors (errors serialized as JSON into `ErrorDetails`), `Failed` on a fatal uncaught exception (with stack trace in `ErrorDetails`).

### Worker (BackgroundService)

The `Worker.cs` entry point resolves scoped services via `IServiceScopeFactory` and drives three modes:

- **Normal mode:** `RunGapDetectionAsync` queries last successful/partial `SyncRun`, backfills missed days, then enters `RunScheduleLoopAsync`.
- **Schedule loop:** `CronExpression.Parse("0 5 * * *", CronFormat.Standard)` with `GetNextOccurrence(now, EasternTime)` — DST-safe because Cronos uses the full `TimeZoneInfo` object. `Task.Delay` until 5am ET, then calls `RunSyncForDateAsync`.
- **Backfill mode:** Triggered by `--backfill` arg or `BACKFILL=true` env var. Processes every day from 2025-10-22 (first game of 2025-26 season) through today, then exits.

OS-aware timezone: `"Eastern Standard Time"` on Windows, `"America/New_York"` on Linux (after tzdata is installed).

### SyncOptions

Simple POCO injected as singleton — `IsBackfill` bool flag drives Worker mode selection.

### Dockerfile tzdata

Added `apt-get install -y --no-install-recommends tzdata` to the `dotnet/runtime:9.0` stage before `ENTRYPOINT`. Without this, `TimeZoneInfo.FindSystemTimeZoneById("America/New_York")` throws `TimeZoneNotFoundException` on Linux containers.

## Task Commits

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | SyncOrchestrator + SyncOptions + Program.cs | `8b2ab1a` | SyncOrchestrator.cs, SyncOptions.cs, Program.cs |
| 2 | Worker.cs Cronos scheduler + Dockerfile tzdata | `58959ac` | Worker.cs, Dockerfile |

## Deviations from Plan

### Minor: Dockerfile location

The plan referenced `nba-lines-tracker/Dockerfile.worker` but the actual file created in Phase 01 is `nba-lines-tracker/src/NbaTracker.Worker/Dockerfile`. The tzdata fix was applied to the correct file. This is an inconsequential naming discrepancy in the plan document.

### Minor: WentToOvertime preservation on updates

The plan spec says `WentToOvertime = bdlGame.Period > 4`. For updates, this was implemented as `existing.WentToOvertime = bdlGame.Period > 4 ? true : existing.WentToOvertime` — preserving an existing `true` value if the API response no longer shows Period > 4 (which could happen for re-fetched final games). This is more correct behavior.

No other deviations — plan executed as written.

## Build Verification

- `dotnet build NbaTracker.Worker.csproj` — **Build succeeded. 0 errors.** (MSB3277 warnings are pre-existing)
- `dotnet build NbaTracker.sln` (full solution) — **Build succeeded. 0 errors.**

## Self-Check: PASSED

Files verified to exist:
- `nba-lines-tracker/src/NbaTracker.Worker/Services/SyncOrchestrator.cs` — FOUND
- `nba-lines-tracker/src/NbaTracker.Worker/SyncOptions.cs` — FOUND
- `nba-lines-tracker/src/NbaTracker.Worker/Worker.cs` — FOUND (replaced)
- `nba-lines-tracker/src/NbaTracker.Worker/Program.cs` — FOUND (updated)
- `nba-lines-tracker/src/NbaTracker.Worker/Dockerfile` — FOUND (updated)

Commits verified:
- `8b2ab1a` — feat(02-04): add SyncOrchestrator full sync pipeline with SyncRun observability
- `58959ac` — feat(02-04): add Cronos 5am ET scheduler, gap detection, backfill mode, tzdata fix

Key invariants verified:
- `CancellationToken.None` appears only in the `finally` block's `SaveChangesAsync` call
- `SaveChangesAsync` appears 5 times in SyncOrchestrator (initial SyncRun save, per BDL game, per Odds game, per team seed, finally block)
- Worker.cs contains `CronExpression`, `IServiceScopeFactory`, and `EasternTime`
- Dockerfile contains `tzdata`
