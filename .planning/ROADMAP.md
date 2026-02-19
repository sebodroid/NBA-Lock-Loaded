# Roadmap: NBA Lines Tracker

## Overview

The build follows a strict dependency chain dictated by the data architecture: schema and Docker scaffolding must exist before the ingestion worker can run, the worker must populate the database with real data before the API can serve meaningful responses, and the API must exist before the React frontend has anything to consume. The multi-panel comparison UI — the primary differentiator — is built in Phase 4 where it can be developed and iterated against real data. Phase 5 makes the working application production-ready.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Foundation** - Docker Compose scaffold, PostgreSQL schema, EF Core migrations, and shared Data project wired up
- [ ] **Phase 2: Ingestion Worker** - .NET Worker Service pulling from BallDontLie and The Odds API, resolving ATS/O/U results, writing verified data to the database
- [ ] **Phase 3: REST API** - ASP.NET Core Minimal API with JWT auth and all team stats endpoints serving real database data
- [ ] **Phase 4: React Frontend** - Login, sortable/filterable team grid, multi-panel comparison, column customization, and all display features
- [ ] **Phase 5: Production Deploy** - Production Docker Compose, Nginx reverse proxy, secrets management, and deployment to chosen host

## Phase Details

### Phase 1: Foundation
**Goal**: The full four-container environment runs locally and the database schema is correct from the start
**Depends on**: Nothing (first phase)
**Requirements**: None (infrastructure precondition — all requirements depend on this phase to exist)
**Success Criteria** (what must be TRUE):
  1. `docker compose up` starts all four containers (API, Worker, Frontend, PostgreSQL) without errors
  2. EF Core migrations apply cleanly to both local PostgreSQL and Aiven, producing the full schema (Teams, Games, GameLines, GameResults, Users, RefreshTokens, SyncRuns)
  3. The shared `NbaTracker.Data` project is referenced by both the API and Worker projects and compiles without errors
  4. Secrets (API keys, DB connection string, JWT secret) are loaded from `.env` and never committed to source control
**Plans**: 2 plans

Plans:
- [x] 01-01-PLAN.md — .NET solution scaffold, four-container Docker Compose with health checks, Dockerfiles, and secrets baseline
- [ ] 01-02-PLAN.md — NbaTracker.Data entity classes, DbContext, EF Core migrations applied to local PostgreSQL and Aiven

### Phase 2: Ingestion Worker
**Goal**: Real NBA game data and betting lines are in the database with verified ATS/O/U calculations, refreshed daily
**Depends on**: Phase 1
**Requirements**: DATA-01, DATA-02, DATA-03, DATA-04, DATA-05, DATA-06
**Success Criteria** (what must be TRUE):
  1. The worker runs on schedule and the `sync_runs` table shows a success record with timestamp after each run
  2. Completed games in the database have ATS results (COVER / LOSS / PUSH) and O/U results (OVER / UNDER / PUSH) that match known results when cross-checked against Action Network or Covers.com for 20+ games
  3. Admin can query `/api/admin/sync-status` and see last run time, success/failure status, and error details
  4. Running the one-time backfill command populates the database with all completed 2024-25 season games and their resolved lines
  5. A sync failure (network error, API timeout) does not corrupt existing data and is recorded with full error details in `sync_runs`
**Plans**: 4 plans

Plans:
- [ ] 02-01-PLAN.md — BallDontLie typed HttpClient with cursor-based pagination, DTOs, and 3-retry resilience
- [ ] 02-02-PLAN.md — The Odds API typed HttpClient, line DTOs, canonical bookmaker selection (FanDuel primary, HardRock fallback)
- [ ] 02-03-PLAN.md — ATS/O/U calculation engine and cross-API game matching service (TDD with xUnit)
- [ ] 02-04-PLAN.md — SyncOrchestrator full pipeline, Cronos 5am ET scheduler, gap detection, backfill mode, SyncRun observability

### Phase 3: REST API
**Goal**: Users and admins can authenticate, and all team stats endpoints return real data from the database
**Depends on**: Phase 2
**Requirements**: AUTH-01, AUTH-02, AUTH-03, AUTH-04
**Success Criteria** (what must be TRUE):
  1. A user can POST to `/api/auth/login` with valid credentials and receive a JWT access token and refresh token
  2. A valid JWT token allows access to protected team stats endpoints; an expired or missing token returns 401
  3. The refresh token endpoint issues a new access token without requiring re-login
  4. An admin can POST to `/api/admin/users` to create a new user account; the endpoint is blocked for non-admin roles
  5. All team stats endpoints (`/api/teams`, `/api/teams/{id}/stats`, `/api/teams/{id}/games`) return correctly structured JSON populated from real database records
**Plans**: 2 plans

Plans:
- [ ] 03-01-PLAN.md — Email migration, JWT bearer middleware, auth endpoints (login/refresh/logout), admin user creation, admin seed, sync-status endpoint
- [ ] 03-02-PLAN.md — Team stats endpoints: GET /api/teams, GET /api/teams/{id}/stats (home/away splits), GET /api/teams/{id}/games (game log)

### Phase 4: React Frontend
**Goal**: Authenticated users can view all team betting performance data, compare teams side by side, and customize their view
**Depends on**: Phase 3
**Requirements**: TEAM-01, TEAM-02, TEAM-03, TEAM-04, TEAM-05, TEAM-06, TEAM-07, GRID-01, GRID-02, GRID-03, GRID-04, GRID-05, GRID-06, PANEL-01, PANEL-02, PANEL-03, PANEL-04, PANEL-05
**Success Criteria** (what must be TRUE):
  1. User can log in on the login page and is redirected to the team grid; unauthenticated requests to the grid redirect to login
  2. The main grid shows all 30 teams with W-L record, current streak, ATS%, and O/U% — sortable by any column and filterable by conference and division
  3. Clicking a team opens a detail panel showing full stats and game-by-game log; clicking another team opens a second panel beside the first without closing it
  4. ATS% cells show green color-coding above the threshold and red below; the page displays a "Last synced: X ago" timestamp
  5. User can show or hide grid columns and the selection persists across page refreshes
**Plans**: TBD

Plans:
- [ ] 04-01: App shell, routing, auth context, and login page
- [ ] 04-02: Team grid (TanStack Table) with sort, filter, column visibility, and color coding
- [ ] 04-03: Multi-panel comparison system (Zustand store, panel components, game log)
- [ ] 04-04: Home/away splits, last 10 games stats, and data freshness indicator

### Phase 5: Production Deploy
**Goal**: The application runs reliably in production with proper secrets management, reverse proxy, and a chosen hosting target
**Depends on**: Phase 4
**Requirements**: None (no unassigned v1 requirements; this phase delivers operational production readiness)
**Success Criteria** (what must be TRUE):
  1. `docker compose -f docker-compose.prod.yml up` starts the application against Aiven PostgreSQL with no local database container
  2. All traffic enters through Nginx — `/api/*` is proxied to the API container, all other routes serve the React SPA
  3. No secrets appear in committed files; all credentials are injected via environment variables at runtime
  4. The application is accessible at a public URL and the daily sync runs successfully in the production environment
**Plans**: TBD

Plans:
- [ ] 05-01: Production Docker Compose, Nginx reverse proxy config, and secrets management
- [ ] 05-02: Deployment to chosen host (Railway, Fly.io, or Azure App Service) with production sync verification

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Foundation | 1/2 | In Progress | - |
| 2. Ingestion Worker | 0/4 | Not started | - |
| 3. REST API | 0/2 | Not started | - |
| 4. React Frontend | 0/4 | Not started | - |
| 5. Production Deploy | 0/2 | Not started | - |
