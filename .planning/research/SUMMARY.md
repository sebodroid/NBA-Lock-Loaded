# Project Research Summary

**Project:** NBA Lines Tracker (NBA ATS/O-U Betting Performance Tracker)
**Domain:** Sports analytics web app — data ingestion, betting line tracking, multi-user SPA
**Researched:** 2026-02-17
**Confidence:** MEDIUM-HIGH

## Executive Summary

The NBA Lines Tracker is a small-group web app for tracking how all 30 NBA teams perform against the spread (ATS) and over/under (O/U) for the current season. The core pattern for this type of app is a data aggregation + analytics platform: a scheduled ingestion worker pulls from external sports APIs daily, stores normalized data in PostgreSQL, and serves it to a React SPA via a .NET REST API. The stack is constrained by requirements (ASP.NET Core, React, PostgreSQL) and all three are strong fits for the use case. The architecture recommendation is four containers — API, Worker, Frontend, and DB — with a hard separation between the ingestion worker and the API server. This prevents long-running data sync jobs from blocking HTTP request handling and is the standard pattern for sports data aggregators.

The most critical technical decisions are in the data layer, not the application layer. The choice of two external APIs — BallDontLie for game scores and The Odds API for betting lines — creates a cross-API matching problem that must be solved at the schema level on day one. Neither API uses a universal game ID standard, so the schema must store native IDs from each provider separately and establish a deterministic canonical join key (team abbreviations + date) to link records. All ATS/O/U calculations must be performed by the backend using stored lines and final scores, never delegated to an external source. This gives the app independence from any single API's uptime and interpretations.

The primary risk areas are data quality and calculation correctness, not application infrastructure. Specifically: cross-API game matching failures, incorrect ATS direction (home vs. away spread perspective), push case handling (3-value enum required, not boolean), and overtime score handling. These are silent bugs that produce subtly wrong analytics — the kind users trust until they compare a result to a known source. The mitigation strategy is to build correctness verification into Phase 1 of the ingestion and calculation layer, before building any UI that surfaces the data.

---

## Key Findings

### Recommended Stack

The stack uses well-established, low-risk technology across the board. The only MEDIUM-confidence component is the scheduled job approach (BackgroundService vs. Hangfire) — start with BackgroundService for simplicity, and migrate to Hangfire only if debugging sync failures becomes painful. The external API layer carries MEDIUM confidence for BallDontLie specifically (verify current rate limits before building the client).

**Core technologies:**

- **.NET 9 / ASP.NET Core Minimal APIs** — backend framework; Minimal APIs preferred over MVC Controllers for CRUD-heavy data apps; straightforward upgrade path to .NET 10 LTS
- **Entity Framework Core 9 + Npgsql 9** — ORM for PostgreSQL; handles schema migrations, native JSONB support for raw API response storage; hybrid with Dapper viable for complex read queries
- **ASP.NET Core JWT Bearer + BCrypt** — authentication; custom Users table, no Identity framework (overkill for a 5-person private app), no external auth provider needed
- **React 18 + Vite 5 + TypeScript** — frontend; Vite replaces CRA (deprecated); no Next.js (SSR unnecessary for private SPA)
- **TanStack Table v8** — headless data grid; MIT license, TypeScript-first, handles ~2,460 rows (30 teams x 82 games) client-side with instant filtering; chosen over AG Grid Community (too heavy)
- **TanStack Query v5 + Axios** — server state management; eliminates Redux for a data-fetching app; per-team caching works naturally with the multi-panel comparison UI
- **Tailwind CSS v3** — utility-first styling; works with headless TanStack Table; verify v4 stable status before starting
- **Zustand** — client UI state for panel management (which panels open, order, layout); TanStack Query handles all server state
- **BallDontLie API (free tier)** — game schedules, scores, team data; free tier sufficient for daily batch sync
- **The Odds API v4 (free tier)** — betting lines (spreads, totals); 500 req/month free tier sufficient if batched efficiently (~60 calls/month); $79/month Starter plan if usage grows
- **Docker + Docker Compose** — four-container local dev; production points to Aiven PostgreSQL externally
- **PostgreSQL 16 on Aiven** — managed database; free tier (5GB) sufficient for this dataset

**Items to verify before starting:** The Odds API current pricing and free tier limits; BallDontLie current rate limits; Tailwind CSS v4 stable status; .NET 10 tooling stability if choosing LTS.

### Expected Features

**Must have (table stakes):**
- All 30 teams with ATS%, O/U%, season W-L record, current streak — the core value proposition
- Sortable, filterable data grid (conference, division, column sort) — users immediately sort by ATS%
- Per-user login — required for invite-only access
- Last 10 games ATS/OU stats — recency matters more than full season for trends
- Home/Away split stats — first filter users request
- Data freshness timestamp — trust signal

**Should have (competitive differentiators):**
- Multi-panel side-by-side team comparison — the explicit differentiating UX request from the user; each panel is an independent component with its own state
- Customizable column visibility per user — reduces noise; stored in localStorage or DB
- Color-coded performance indicators (green/red at ATS thresholds)
- ATS trend line (last 10 vs season) — spot covering runs vs cold streaks; add after validation

**Defer (v2+):**
- Player prop tracking (confirmed deferred by user)
- Alerts/push notifications
- Live in-game odds updates (WebSocket complexity + API cost spike; daily batch produces cleaner data)
- Win probability / prediction model (ML infrastructure scope explosion)
- Social features (comments, picks)
- Multi-season historical comparison

**Feature dependency chain:** Both APIs must ingest successfully before any ATS/O/U stats can be displayed. Auth is required before column customization can be user-specific. The data ingestion layer is the critical path — nothing meaningful can be tested in the UI until games + lines are in the database.

### Architecture Approach

The recommended architecture is four discrete containers sharing a single PostgreSQL database: a React SPA served via Nginx, an ASP.NET Core Web API handling all HTTP traffic and auth, a separate .NET Worker Service handling all external API ingestion and ATS/O/U result resolution, and PostgreSQL on Aiven. The API never calls external sports APIs directly; the worker never serves HTTP traffic. This clean separation means the daily sync job (which can take minutes and may encounter rate limiting or retries) cannot block API response times, and each container can be restarted independently. The shared `NbaTracker.Data` class library (EF Core entities, DbContext, migrations) is referenced by both the API and Worker projects.

**Major components:**

1. **React SPA (Nginx-served)** — all user interaction; Zustand for panel UI state; TanStack Query for server state caching; TanStack Table for the sortable/filterable data grid; multi-panel comparison is the core differentiating UX element
2. **ASP.NET Core Web API** — thin REST layer; JWT auth; reads pre-computed data from DB; 7 endpoints covering auth, teams aggregate stats, team game logs, and admin sync status
3. **.NET Worker Service** — daily scheduled ingestion; calls BallDontLie for game data and The Odds API for lines; upserts games/scores; resolves ATS and O/U results after final scores arrive; writes sync outcomes to `sync_runs` table
4. **PostgreSQL (Aiven)** — normalized schema: Teams, Games, GameLines (append-only, opening/closing flags), GameResults (resolved ATS/O/U), Users, RefreshTokens, SyncRuns

**Schema design principle:** Normalize teams and games; denormalize ATS/O/U result columns onto `game_results` for fast reads; treat odds as append-only rows (never update in place) to preserve line movement history. Store both `nba_api_game_id` and `odds_api_game_id` on every game; use a canonical join key of `{season}_{home_abbr}_{away_abbr}_{game_date_utc}` for cross-API matching.

**Build order dependency:** Ingestion worker must run successfully and populate the DB with real data before any meaningful API or frontend development can be tested. Build and validate ingestion first.

### Critical Pitfalls

1. **Cross-API game ID mismatch (PITFALL-01)** — BallDontLie and The Odds API use completely different game identifiers; naive joins return nothing or worse, silently match wrong games. Store each API's native ID in separate columns and build a deterministic canonical key from team abbreviations + game date. Write a matching service that flags unmatched records; never silently discard them.

2. **ATS direction error (PITFALL-06)** — The spread is stored from one team's perspective; if home/away perspective is applied incorrectly for road favorites, every affected ATS result is flipped. Always store `favorite_team_id` and `is_home_team_favorite` explicitly. Write unit tests against known historical results and cross-check 20+ games against Action Network or Covers.com before trusting the calculation engine.

3. **Push cases ignored (PITFALL-07)** — Integer spreads can result in an exact cover, which is a push (bettors get money back). Storing ATS result as a boolean corrupts all analytics. ATS and O/U results must be a three-value enum: COVER/LOSS/PUSH.

4. **Odds data overwritten in place (PITFALL-12)** — If you UPDATE an odds row when a line moves, you lose opening line data and cannot reconstruct ATS history. Treat all odds records as append-only; use `is_opening_line` and `is_closing_line` boolean flags; only the closing line record is used for ATS calculation.

5. **No sync observability (PITFALL-18)** — A silent sync failure means stale data that users trust for days. Write every sync outcome (success or failure, with full error text) to a `sync_runs` table. Expose `/api/admin/sync-status` so the state is always queryable without log diving.

---

## Implications for Roadmap

Based on the combined research, the build dependency chain is clear: schema and ingestion must come before the API, and the API must come before the frontend. The multi-panel comparison UI can only be meaningfully tested against real data. The suggested phase structure follows the architecture's natural build order.

### Phase 1: Foundation and Schema

**Rationale:** All subsequent phases depend on the schema being correct. Pitfall research identifies 13 pitfalls that must be addressed at the schema level — fixing these post-data is expensive. This phase also wires up Docker Compose and the shared Data project so both API and Worker have something to build against.
**Delivers:** Working Docker Compose scaffold with all four containers; EF Core schema with migrations applied to local and Aiven; shared `NbaTracker.Data` project; `.env` secrets management in place from day one.
**Addresses:** PITFALL-01 (dual external IDs + canonical key), PITFALL-11 (TIMESTAMPTZ everywhere), PITFALL-12 (append-only odds schema), PITFALL-07 (3-value enum for results), PITFALL-13 (game status enum with postponed handling), PITFALL-14 (indexes), PITFALL-15 (Docker health check for PostgreSQL startup), PITFALL-17 (secrets in .env, not committed).
**Research flag:** Standard patterns — no deeper research needed. EF Core migrations and Docker Compose setup are well-documented.

### Phase 2: Data Ingestion Worker

**Rationale:** No real data, no real development. Getting the Worker running and producing verified data in the DB is the single highest-leverage phase. Every downstream phase tests against real data from this point. Build this before any API endpoint or UI work.
**Delivers:** Daily-scheduled .NET Worker Service that ingests NBA game schedules and scores from BallDontLie, ingests betting lines from The Odds API, resolves ATS/O/U results after games go final, and writes sync outcomes to `sync_runs`. Full season backfill on first run.
**Implements:** `INbaApiClient`, `IOddsApiClient` (typed HttpClient wrappers with Polly retry), `DailyIngestionWorker` (PeriodicTimer loop), cross-API game matching service with canonical key, completeness check after each sync run.
**Addresses:** PITFALL-03 (re-validation window for final scores), PITFALL-04 (idempotent resumable sync with `sync_jobs` table), PITFALL-05 (sync odds for next 7 days, not just today), PITFALL-06 (ATS direction logic with unit tests), PITFALL-08 (OT-inclusive final scores), PITFALL-09 (canonical sportsbook for ATS line), PITFALL-23 (graceful postponement handling mid-run).
**Research flag:** Needs validation — verify BallDontLie and The Odds API current rate limits and response shapes before building clients. The free tier math in STACK.md checks out (~60 calls/month) but confirm against current API documentation. Historical backfill for 2024-25 season may require one paid month of The Odds API Starter ($79).

### Phase 3: REST API

**Rationale:** With real data in the database, the API endpoints can be built and immediately validated against actual ATS/O/U records. Auth is built here because it gates all subsequent frontend work.
**Delivers:** Seven REST endpoints covering auth (login/register), teams aggregate stats, team game log with lines and results, and admin sync status. JWT Bearer auth with BCrypt password hashing. Refresh token rotation with server-side storage.
**Implements:** `NbaTracker.Api` project with Minimal APIs; `TeamStatsService` (ATS%, O/U% computation); JWT middleware; thin controller pattern (controllers call services, return DTOs); CORS configured for React dev origin.
**Addresses:** PITFALL-19 (JWT secret from env, 256-bit minimum), PITFALL-20 (issuer/audience validation), PITFALL-21 (refresh tokens stored server-side with revocation), PITFALL-22 (admin endpoints require Admin role from day one).
**Research flag:** Standard patterns — JWT Bearer in ASP.NET Core Minimal APIs is well-documented. The ATS/O/U computation logic should be cross-checked against known results before this phase closes.

### Phase 4: React Frontend

**Rationale:** With API endpoints returning real data, all frontend work can be built and tested against production-quality responses. The multi-panel comparison UI is the primary differentiator and should be built early in this phase to allow iteration time.
**Delivers:** Login page with JWT token handling; main team grid (TanStack Table, sortable, filterable by conference/division); multi-panel comparison system (Zustand store, independent panel components, each fetching their own data via TanStack Query); home/away split stats display; last 10 games filter; column customization; data freshness indicator; color-coded ATS/O/U performance indicators.
**Implements:** Zustand panel store; TanStack Query hooks per team; TanStack Table with column filter/sort; Tailwind CSS layout (grid/flex for multi-panel); protected routes with JWT; Vite proxy for `/api` to avoid CORS in development.
**Addresses:** PITFALL-16 (Vite proxy for Docker networking, service names not localhost).
**Research flag:** The multi-panel comparison UX has no established pattern to reference — it is custom to this app. This is the phase most likely to need iteration. Plan for UI refinement time. No deeper research needed on the library choices.

### Phase 5: Production Deploy and Polish

**Rationale:** Everything works locally; this phase makes it production-ready. Column customization (localStorage or DB-persisted) and deployment configuration are polish items that should not block core functionality.
**Delivers:** Production Docker Compose (no local db service, Aiven connection string); Nginx reverse proxy config (single-origin, `/api/*` proxied); production secrets via environment variables; column customization per-user (localStorage-persisted initially, DB-persisted if users request cross-device sync); Serilog structured logging; deployment to chosen host (Railway, Fly.io, or Azure App Service).
**Addresses:** Remaining infra concerns; monitoring via `sync_runs` table; Hangfire dashboard if BackgroundService proves insufficient for debugging.
**Research flag:** Deployment target not yet chosen — Railway, Fly.io, and Azure App Service are all viable for this scale. Needs a decision before this phase. Standard patterns otherwise.

### Phase Ordering Rationale

- Schema must precede ingestion because the pitfalls research identifies 13 schema-level issues; retrofitting schema after data exists is painful.
- Ingestion must precede the API because API endpoints returning empty or test data cannot be meaningfully tested or debugged.
- API must precede the frontend because the frontend has no useful data source until auth and the team stats endpoint exist.
- Deploy polish is last because production-readiness concerns (Nginx config, secret management, hosting) do not block development.
- Multi-panel comparison is placed in Phase 4 (not deferred to Phase 5) because it is the primary UX differentiator that needs iteration time, and it depends only on the teams API endpoint being available.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 2 (Ingestion Worker):** Validate BallDontLie and The Odds API current response schemas and rate limits before building typed HTTP clients. The research reflects known API behavior through mid-2025; confirm nothing has changed. Also confirm whether The Odds API historical odds endpoint is available on the free tier or requires the $79 Starter plan for backfill.
- **Phase 5 (Deploy):** Deployment target is undecided. Evaluate Railway, Fly.io, and Azure App Service for .NET Worker Service support (background services need persistent processes, not serverless). This is a non-trivial constraint.

Phases with standard patterns (skip research-phase):
- **Phase 1 (Foundation/Schema):** EF Core migrations, Docker Compose, and PostgreSQL schema design are well-documented with extensive community resources.
- **Phase 3 (API):** ASP.NET Core Minimal APIs with JWT Bearer is well-documented. The computation logic is application-specific but not a research question.
- **Phase 4 (Frontend):** TanStack Table, TanStack Query, and Zustand all have strong documentation. The Vite + Tailwind setup is standard.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All major technology choices are well-established with one MEDIUM exception (BackgroundService vs. Hangfire — start simple, migrate if needed). Verify Tailwind v4 stable status and .NET 10 tooling if switching from .NET 9. |
| Features | HIGH | Feature set is well-defined with clear P1/P2/P3 prioritization. MVP scope is reasonable. Multi-panel comparison is explicitly requested and architecturally understood. |
| Architecture | HIGH | Four-container pattern with API/Worker separation is a strong, proven approach for data aggregation apps. Schema design is specific and correct for the domain. The build order dependency chain is clear. |
| Pitfalls | HIGH | 24 specific pitfalls identified with concrete prevention strategies. The top 5 "build this first" items are clearly ranked. Cross-API matching and ATS calculation correctness pitfalls are domain-specific and well-researched. |

**Overall confidence:** HIGH

### Gaps to Address

- **Deployment target (Phase 5):** Not decided. Evaluate Railway, Fly.io, and Azure App Service specifically for .NET Worker Service support (persistent background process, not serverless). This needs a decision before Phase 5 planning.
- **API rate limit verification (Phase 2):** BallDontLie and The Odds API free tier limits should be confirmed at their current documentation before building ingestion clients. The STACK.md math shows free tier is sufficient, but API pricing changes.
- **Historical odds backfill strategy (Phase 2):** The Odds API historical endpoint requires a paid plan. Budget ~$79 for one month of Starter plan to backfill the 2024-25 season on launch, or accept that the app launches with current-season data only going forward from the launch date.
- **Tailwind CSS v4 status:** Was in alpha mid-2025. If v4 is now stable, evaluate whether to use v4 instead of v3. The recommendation defaults to v3 as the safe choice.
- **Register endpoint access model:** Research recommends admin-only registration (no self-serve, no email verification). Confirm this is the desired user onboarding flow for the friend group before building auth.
- **Canonical sportsbook selection (Phase 2/3):** Which sportsbook's line to use as the canonical ATS calculation source (e.g., DraftKings, consensus) must be decided before the ingestion worker is built. This is a config value but needs a decision.

---

## Sources

### Primary (HIGH confidence)
- Official .NET 9 / ASP.NET Core documentation — Minimal APIs, JWT Bearer, BackgroundService patterns
- TanStack Table v8 documentation — headless grid API, filtering, sorting
- TanStack Query v5 documentation — useQuery, useMutation, query key patterns
- The Odds API v4 documentation — basketball_nba sport key, spreads/totals markets, free tier request math
- PostgreSQL 16 documentation — TIMESTAMPTZ, partial indexes, EXPLAIN ANALYZE

### Secondary (MEDIUM confidence)
- BallDontLie API documentation — game scores, team data, season filtering (verify current rate limits)
- Zustand documentation — panel store pattern for client UI state
- Hangfire PostgreSQL documentation — dashboard, retry patterns
- Covers.com / Action Network / Basketball-Reference — feature conventions, ATS/O/U display patterns, cross-check source for calculation verification

### Tertiary (LOW confidence — verify before relying on)
- The Odds API historical odds endpoint availability on free vs. paid tiers — needs direct verification
- Aiven PostgreSQL free tier current limits (5GB referenced in research; confirm current offering)
- Tailwind CSS v4 stable release status (was alpha mid-2025; research defaults to v3)

---
*Research completed: 2026-02-17*
*Ready for roadmap: yes*
