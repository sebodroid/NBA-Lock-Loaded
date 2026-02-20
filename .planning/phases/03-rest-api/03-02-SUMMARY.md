---
phase: 03-rest-api
plan: 02
subsystem: api
tags: [aspnetcore, minimal-api, efcore, postgresql, team-stats, ats, ou]

# Dependency graph
requires:
  - phase: 01-foundation
    provides: NbaTrackerDbContext, Game, Team, GameResult, GameLine entities, EF migrations
  - phase: 02-ingestion-worker
    provides: populated Games, Teams, GameResults, GameLines tables from sync worker
  - phase: 03-rest-api/plan-01
    provides: JWT bearer auth middleware, RequireAuthorization, NbaTrackerDbContext DI, route group pattern
provides:
  - GET /api/teams — all teams with aggregate ATS/OU stats (TeamStatsResponse array)
  - GET /api/teams/{id}/stats — home/away ATS and O/U splits for one team (TeamDetailResponse)
  - GET /api/teams/{id}/games — game log sorted descending by date (GameLogEntry array)
  - 401 for unauthenticated requests, 404 for unknown team IDs
affects: [04-frontend, deployment]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Enum comparisons (AtsResult, OuResult) performed in C# after ToListAsync — EF Core HasConversion<string>() enums cannot be compared in IQueryable LINQ
    - Single bulk query for GET /api/teams (all FINAL games loaded once, partitioned in C# per team)
    - Targeted queries for GET /api/teams/{id}/stats (two queries: home games, away games separately)
    - DTO projection in C# using .Select after .ToListAsync — never inside IQueryable Select

key-files:
  created:
    - nba-lines-tracker/src/NbaTracker.Api/Models/TeamModels.cs
    - nba-lines-tracker/src/NbaTracker.Api/Endpoints/TeamEndpoints.cs
  modified:
    - nba-lines-tracker/src/NbaTracker.Api/Program.cs

key-decisions:
  - "Enum comparisons for AtsResult/OuResult must happen in C# after ToListAsync — HasConversion<string>() prevents EF Core from translating enum == comparisons to SQL"
  - "GET /api/teams loads all FINAL games in one query and partitions in memory per team — acceptable at ~2,460 rows max, avoids 30 separate DB queries"
  - "O/U stats counted from both homeGames + awayGames union per team — OuResult is per-game and each team appears as either home or away, not both"
  - "TeamEndpoints.Map registered under RequireAuthorization() (no AdminOnly policy) — team data is for all authenticated users, not admin-only"

patterns-established:
  - "Bulk query + in-memory partition: load all rows once, partition by team in C# to avoid N+1 queries for aggregate stats"
  - "C#-only enum projection: always materialize with ToListAsync before accessing enum values or calling .ToString() on them"

requirements-completed: [AUTH-02]

# Metrics
duration: 15min
completed: 2026-02-19
---

# Phase 3 Plan 02: Team Stats Endpoints Summary

**Three JWT-protected team endpoints (list/stats/games) returning real ATS and O/U data from the database, with all enum comparisons in C# after materialization to avoid EF Core LINQ translation errors**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-02-19T00:00:00Z
- **Completed:** 2026-02-19T00:15:00Z
- **Tasks:** 1
- **Files modified:** 3

## Accomplishments
- TeamModels.cs with four response DTOs: TeamStatsResponse, HomeAwaySplit, TeamDetailResponse, GameLogEntry
- TeamEndpoints.cs: GET /api/teams returns array of all teams with aggregate win/loss/ATS/OU stats; GET /api/teams/{id}/stats returns home/away splits; GET /api/teams/{id}/games returns full game log descending by date
- All three endpoints protected by RequireAuthorization() — 401 for unauthenticated, 404 for unknown team IDs
- All AtsResult and OuResult enum comparisons performed in C# after ToListAsync() — no EF Core LINQ translation exceptions

## Task Commits

Each task was committed atomically:

1. **Task 1: TeamEndpoints and response models** - `09e25fc` (feat)

**Plan metadata:** (docs commit — created after state updates)

## Files Created/Modified
- `nba-lines-tracker/src/NbaTracker.Api/Models/TeamModels.cs` - TeamStatsResponse, HomeAwaySplit, TeamDetailResponse, GameLogEntry response DTOs
- `nba-lines-tracker/src/NbaTracker.Api/Endpoints/TeamEndpoints.cs` - GET /api/teams, GET /api/teams/{id}/stats, GET /api/teams/{id}/games with C#-side enum comparisons
- `nba-lines-tracker/src/NbaTracker.Api/Program.cs` - TeamEndpoints.Map wired to /api/teams route group with RequireAuthorization()

## Decisions Made
- AtsResult and OuResult enum comparisons must happen in C# after .ToListAsync(). These enums use HasConversion<string>() in EF Core, meaning EF cannot translate enum == comparisons into SQL — attempting to do so throws InvalidOperationException: The LINQ expression could not be translated.
- GET /api/teams loads all FINAL games in a single bulk query and partitions per team in memory — acceptable for the maximum dataset size (~2,460 games per season) and avoids 30 separate round-trips.
- RequireAuthorization() (not AdminOnly) on the /teams route group — team data is intended for all logged-in users, not just admins.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- MSB3277 warnings about EF Core version conflict in NbaTracker.Worker — pre-existing issue documented in STATE.md, build succeeded with 0 errors.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 3 complete: all five ROADMAP success criteria are satisfied
  1. POST /api/auth/login returns 200 with accessToken + refreshToken
  2. GET /api/teams with access token returns 200; without token returns 401
  3. POST /api/auth/refresh returns new accessToken
  4. POST /api/admin/users (admin JWT) creates user; non-admin returns 403
  5. GET /api/teams, /api/teams/{id}/stats, /api/teams/{id}/games return correctly structured JSON
- Phase 4 (Frontend) can now consume all team endpoints and auth endpoints
- No outstanding blockers for Phase 4

## Self-Check: PASSED

All created files verified present on disk. Task commit 09e25fc verified in git log.

---
*Phase: 03-rest-api*
*Completed: 2026-02-19*
