---
phase: 02-ingestion-worker
plan: 01
subsystem: api
tags: [balldontlie, httpclient, resilience, pagination, dotnet]

# Dependency graph
requires:
  - phase: 01-data-foundation
    provides: NbaTracker.Data entities (Game, Team, GameLine, GameResult) that BDL DTOs map to
provides:
  - BdlGame DTO with all BallDontLie v1 API response fields
  - BdlTeam DTO with Id, Abbreviation, FullName
  - BdlPagedResponse<T> cursor-based pagination wrapper
  - BallDontLieClient typed HttpClient with GetGamesAsync + GetTeamsAsync
  - 3-retry exponential backoff via AddResilienceHandler in Program.cs
affects: [02-03, 02-04]

# Tech tracking
tech-stack:
  added: [Cronos 0.9.*, Microsoft.Extensions.Http.Resilience 9.*]
  patterns: [typed-httpclient, cursor-based-pagination, resilience-pipeline]

key-files:
  created:
    - nba-lines-tracker/src/NbaTracker.Worker/Models/BallDontLie/BdlGame.cs
    - nba-lines-tracker/src/NbaTracker.Worker/Models/BallDontLie/BdlTeam.cs
    - nba-lines-tracker/src/NbaTracker.Worker/Models/BallDontLie/BdlPagedResponse.cs
    - nba-lines-tracker/src/NbaTracker.Worker/Services/BallDontLieClient.cs
  modified:
    - nba-lines-tracker/src/NbaTracker.Worker/NbaTracker.Worker.csproj

key-decisions:
  - "OddsApi DTOs were bundled into the first commit alongside BDL DTOs — executor created all 6 DTO files in one pass"
  - "API key passed via Authorization header (Bearer token pattern for BallDontLie v1)"
  - "Cursor loop uses int? NextCursor — null signals end of pages"
  - "No rate-limit delay in client — SyncOrchestrator controls pacing per plan spec"

patterns-established:
  - "Typed HttpClient: AddHttpClient<TClient> + AddResilienceHandler — use this pattern for all external API clients"
  - "Cursor pagination: accumulate all pages before returning — callers get complete list, not page-by-page"
  - "JsonPropertyName on all DTO properties — snake_case API fields map to PascalCase C# properties"

requirements-completed: [DATA-01]

# Metrics
duration: 30min
completed: 2026-02-18
---

# Phase 02-01: BallDontLie API Client Summary

**BallDontLie typed HttpClient with cursor-based pagination, exponential-backoff retry (3x), and GetGamesAsync/GetTeamsAsync for NBA schedule ingestion**

## Performance

- **Duration:** ~30 min
- **Completed:** 2026-02-18
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- BdlGame, BdlTeam, BdlPagedResponse<T> DTOs with JsonPropertyName attributes matching BallDontLie v1 API schema
- BallDontLieClient typed HttpClient registered in DI with AddResilienceHandler: 3-retry, exponential backoff (2s base + jitter), 30s timeout
- Cursor-based pagination loop in GetGamesAsync — accumulates all pages before returning
- GetTeamsAsync for seeding the Teams table on first run
- Added Cronos 0.9.* and Microsoft.Extensions.Http.Resilience 9.* NuGet packages

## Task Commits

1. **Task 1: BDL DTOs and NuGet packages** - `d41d9da` (feat)
2. **Task 2: BallDontLieClient typed HttpClient** - `7042899` (feat)

## Files Created/Modified
- `nba-lines-tracker/src/NbaTracker.Worker/Models/BallDontLie/BdlGame.cs` — Game DTO with Period, Postponed, scores, nested team refs
- `nba-lines-tracker/src/NbaTracker.Worker/Models/BallDontLie/BdlTeam.cs` — Team DTO with Id, Abbreviation, FullName
- `nba-lines-tracker/src/NbaTracker.Worker/Models/BallDontLie/BdlPagedResponse.cs` — Cursor pagination wrapper with NextCursor
- `nba-lines-tracker/src/NbaTracker.Worker/Services/BallDontLieClient.cs` — Typed HttpClient, cursor loop, retry handler
- `nba-lines-tracker/src/NbaTracker.Worker/NbaTracker.Worker.csproj` — Cronos + Http.Resilience packages added

## Decisions Made
- OddsApi DTOs were committed alongside BDL DTOs in Task 1 (executor created all 6 DTO files in one pass — acceptable deviation, both sets needed)
- No rate-limit delay inside client per plan spec — orchestrator handles pacing

## Deviations from Plan
None - plan executed as specified (OddsApi DTO bundling was a commit organization choice, not a functional deviation).

## User Setup Required
- **BallDontLie__ApiKey** env var required for real runs. Source: BallDontLie dashboard → Account → API Key.

## Next Phase Readiness
- BallDontLieClient ready for SyncOrchestrator (02-04) to call
- GetTeamsAsync ready for team seed logic in SyncOrchestrator

---
*Phase: 02-ingestion-worker*
*Completed: 2026-02-18*
