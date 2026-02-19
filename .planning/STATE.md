# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-17)

**Core value:** At a glance, see which NBA teams cover the spread and hit the over/under most reliably
**Current focus:** Phase 2 — Data Sync

## Current Position

Phase: 2 of 5 (Data Sync)
Plan: 0 of N in current phase
Status: Phase 1 complete — ready to start Phase 2
Last activity: 2026-02-18 — Completed Plan 01-02: Entity schema, EF Core migrations, local + Aiven PostgreSQL initialized

Progress: [██░░░░░░░░] 20%

## Performance Metrics

**Velocity:**
- Total plans completed: 2
- Average duration: 18 min
- Total execution time: 0.6 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01-foundation | 2/2 | 36 min | 18 min |

**Recent Trend:**
- Last 5 plans: 6 min, 30 min
- Trend: varies by complexity

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
- 01-02: ATS/O/U enums as 3-value Cover/Loss/Push, Over/Under/Push stored as text — boolean would corrupt push-case analytics
- 01-02: FavoriteTeamId explicit FK in GameLines — spread sign alone cannot determine favorite (same absolute value for both sides)
- 01-02: Game.WentToOvertime as nullable bool — required for O/U push detection when final score hits the total exactly
- 01-02: TokenHash in RefreshToken stores BCrypt hash of token, not plaintext — security requirement
- 01-02: .dockerignore required at solution root — host obj/project.assets.json contains Windows VS paths that break Linux Docker builds
- 01-02: Local migration via idempotent SQL script — Docker Desktop on Windows routes localhost:5432 through WSL2 NAT, bypassing pg_hba.conf trust rules; SCRAM auth fails from host; unix socket (docker exec) works

### Pending Todos

None yet.

### Blockers/Concerns

- Phase 2: Verify BallDontLie and The Odds API current rate limits and response schemas before building clients
- Phase 2: Confirm whether The Odds API historical endpoint is available on free tier or requires $79/month Starter plan for 2024-25 season backfill
- Phase 5: Deployment target not decided — evaluate Railway, Fly.io, and Azure App Service for .NET Worker Service support (persistent background process required, not serverless)
- Local dev: `dotnet ef database update` from Windows host cannot connect to Docker PostgreSQL via TCP (WSL2 NAT + SCRAM auth). Use SQL script + docker exec approach for future local migrations.

## Session Continuity

Last session: 2026-02-18
Stopped at: Phase 2 plans complete — ready to execute
Resume file: .planning/phases/02-ingestion-worker/02-01-PLAN.md
