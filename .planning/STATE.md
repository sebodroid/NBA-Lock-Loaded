# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-17)

**Core value:** At a glance, see which NBA teams cover the spread and hit the over/under most reliably
**Current focus:** Phase 1 — Foundation

## Current Position

Phase: 1 of 5 (Foundation)
Plan: 0 of 2 in current phase
Status: Ready to plan
Last activity: 2026-02-17 — Roadmap created; ready to begin Phase 1 planning

Progress: [░░░░░░░░░░] 0%

## Performance Metrics

**Velocity:**
- Total plans completed: 0
- Average duration: — min
- Total execution time: 0 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| - | - | - | - |

**Recent Trend:**
- Last 5 plans: —
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

### Pending Todos

None yet.

### Blockers/Concerns

- Phase 2: Verify BallDontLie and The Odds API current rate limits and response schemas before building clients
- Phase 2: Confirm whether The Odds API historical endpoint is available on free tier or requires $79/month Starter plan for 2024-25 season backfill
- Phase 5: Deployment target not decided — evaluate Railway, Fly.io, and Azure App Service for .NET Worker Service support (persistent background process required, not serverless)

## Session Continuity

Last session: 2026-02-17
Stopped at: Roadmap created; STATE.md initialized; REQUIREMENTS.md traceability updated
Resume file: None
