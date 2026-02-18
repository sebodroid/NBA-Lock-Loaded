# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-17)

**Core value:** At a glance, see which NBA teams cover the spread and hit the over/under most reliably
**Current focus:** Phase 1 — Foundation

## Current Position

Phase: 1 of 5 (Foundation)
Plan: 1 of 2 in current phase
Status: In progress
Last activity: 2026-02-18 — Completed Plan 01-01: Docker Compose scaffold and .NET solution structure

Progress: [█░░░░░░░░░] 10%

## Performance Metrics

**Velocity:**
- Total plans completed: 1
- Average duration: 6 min
- Total execution time: 0.1 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01-foundation | 1/2 | 6 min | 6 min |

**Recent Trend:**
- Last 5 plans: 6 min
- Trend: —

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- Roadmap: Four-container architecture (API, Worker, Frontend, DB) — Worker and API are separate containers so sync jobs cannot block HTTP responses
- Roadmap: BallDontLie API for game data, The Odds API for betting lines — cross-API matching uses canonical key: `{season}_{home_abbr}_{away_abbr}_{game_date_utc}`
- Roadmap: FanDuel as primary canonical sportsbook for ATS line (HardRock as fallback) — configured in worker, not hardcoded
- Roadmap: ATS/O/U results stored as 3-value enums (COVER/LOSS/PUSH, OVER/UNDER/PUSH) — boolean would corrupt push-case analytics
- Roadmap: Admin-only user creation (no self-serve registration) — small invite-only friend group
- 01-01: Solution-root Docker build context (context: .) — only way to COPY NbaTracker.Data in Dockerfiles; wrong context requires full restructure
- 01-01: dotnet/runtime:9.0 for Worker container — no HTTP server, ~60MB smaller than aspnet image
- 01-01: NbaTrackerDbContext stub created in Plan 01-01 so Api/Worker compile — full schema deferred to 01-02
- 01-01: MigrateAsync() wrapped in try/catch in Development — allows API container startup before migrations exist

### Pending Todos

None yet.

### Blockers/Concerns

- Phase 2: Verify BallDontLie and The Odds API current rate limits and response schemas before building clients
- Phase 2: Confirm whether The Odds API historical endpoint is available on free tier or requires $79/month Starter plan for 2024-25 season backfill
- Phase 5: Deployment target not decided — evaluate Railway, Fly.io, and Azure App Service for .NET Worker Service support (persistent background process required, not serverless)
- Plan 01-01: Docker Desktop must be running to verify docker compose build/up — runtime verification deferred

## Session Continuity

Last session: 2026-02-18
Stopped at: Completed 01-01-PLAN.md
Resume file: None
