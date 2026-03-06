# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-17)

**Core value:** At a glance, see which NBA teams cover the spread and hit the over/under most reliably
**Current focus:** Phase 4 — Frontend

## Current Position

Phase: 4 of 5 (React Frontend) — IN PROGRESS
Plan: 04-at-checkpoint (of 4 total)
Status: Plan 04-04 Task 1 complete — MainPage wired with TeamGrid, PanelStrip, MatchupsSection, sticky header (last-synced, theme toggle, logout), nginx proxy. Awaiting human visual verification (Task 2 checkpoint).
Last activity: 2026-03-06 — Executed Plan 04-04 Task 1: MainPage composition, App.tsx wiring, nginx /api/ proxy. Build passes 0 errors. Stopped at checkpoint:human-verify.

Progress: [█████████░] 88%

## Performance Metrics

**Velocity:**
- Total plans completed: 8
- Average duration: ~23 min
- Total execution time: ~3.1 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01-foundation | 2/2 | 36 min | 18 min |
| 02-ingestion-worker | 4/4 | ~79 min | ~20 min |
| 03-rest-api | 2/2 | ~76 min | ~38 min |
| 04-react-frontend | 3/4 | ~19 min | ~6.3 min |

**Recent Trend:**
- Last 5 plans: ~18 min, 25 min, 61 min, 15 min, 5 min
- Trend: 04-01 very fast (3 tasks, mostly writes); 04-02 fast (2 tasks, 4 files, 0 deviations)

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
- 02-03: FavoriteTeamId drives all ATS calculations — never spread sign (spread is always stored as absolute value from favorite's perspective)
- 02-03: ET date conversion uses TimeZoneInfo.ConvertTime for DST safety — a game at 00:30 UTC on Nov 1 is Oct 31 in ET
- 02-03: Flip(Push) = Push — both sides get Push result when favorite margin equals spread exactly
- 02-04: SaveChangesAsync(CancellationToken.None) in finally — SyncRun status always persisted even on cancellation
- 02-04: Per-game SaveChangesAsync — no giant transaction, so partial success is safe
- 02-04: IServiceScopeFactory per sync run — scoped DbContext resolved safely from singleton BackgroundService
- 02-04: Season from date: month >= 10 => year, else year - 1 (handles Jan-Sep cross-year games)
- 03-01: ASP.NET Core env var config key mapping: JWT__Secret env var becomes JWT:Secret config key via __ separator; use config["JWT:Secret"] not config["JWT__Secret"]
- 03-01: BCrypt timing-safe login: always call BCrypt.Verify even when user not found (with dummy hash) to prevent timing-based email enumeration
- 03-01: Refresh token BCrypt hash storage: plaintext returned to client once, TokenHash in DB stores BCrypt hash; candidates fetched by time window and verified in memory (cannot query by hash)
- 03-01: Token rotation: old refresh token RevokedAt set before new token issued — prevents replay attacks
- 03-01: ClockSkew = TimeSpan.Zero: 15-minute access tokens expire exactly on time, no default 5-minute leeway
- 03-01: Admin seed runs only in Development environment — requires ASPNETCORE_ENVIRONMENT=Development in .env for local Docker
- 03-01: Route group auth gating: api.MapGroup("/admin").RequireAuthorization("AdminOnly") — no per-endpoint auth needed
- [Phase 03-rest-api]: Enum comparisons for AtsResult/OuResult must happen in C# after ToListAsync — HasConversion<string>() prevents EF Core from translating enum == comparisons to SQL
- [Phase 03-rest-api]: GET /api/teams loads all FINAL games in one bulk query and partitions in C# per team — avoids 30 separate DB queries, acceptable at ~2,460 rows max
- 04-01: Access token in module memory (not localStorage) for XSS mitigation; refresh token in localStorage for persistence across page reloads
- 04-01: Axios 401 interceptor with in-flight queue — isRefreshing flag + failedQueue array prevents thundering herd on token expiry
- 04-01: Zustand partialize: only theme persisted, auth state reset on reload and restored via tryRestoreSession useEffect
- 04-01: shadcn init with Tailwind v4 requires pre-existing index.css with @import "tailwindcss" — shadcn detects v4 and writes oklch CSS variables
- [Phase 04-react-frontend]: Pre-filter data before TanStack Table for conference/division — avoids dual-filter row model issues
- [Phase 04-react-frontend]: teamColumns has 9 entries: 7 visible + 2 hidden; team column has enableHiding: false
- 04-03: teamAbbr prop kept in GameLog with void suppression — preserves public API for future score-column highlighting without TS error
- 04-03: PanelStrip returns null (not empty div) when openPanels is empty — no layout space consumed when no panels open
- 04-03: id=panel-{teamId} on TeamPanel Card root enables Zustand openPanel scrollIntoView targeting
- 04-04: Popover.Root used for column visibility instead of DropdownMenu — Popovers stay open on item click enabling multi-column toggling without dismissal
- 04-04: lastSyncedAt read from teams[0].lastSyncedAt — all 30 team records share same sync timestamp, reading index 0 is safe and avoids additional API call

### Pending Todos

None yet.

### Blockers/Concerns

- Phase 2: Verify BallDontLie and The Odds API current rate limits and response schemas before building clients
- Phase 2: Confirm whether The Odds API historical endpoint is available on free tier or requires $79/month Starter plan for 2024-25 season backfill
- Phase 5: Deployment target decided — DigitalOcean Droplet ($12/month, 2GB RAM) for API + Worker containers; Cloudflare Pages (free) for React SPA static files; Aiven PostgreSQL already handles DB
- Local dev: `dotnet ef database update` from Windows host cannot connect to Docker PostgreSQL via TCP (WSL2 NAT + SCRAM auth). Use SQL script + docker exec approach for future local migrations.
- 02-Worker: MSB3277 warnings about EF Core version conflict (9.0.1 vs 9.0.13) in NbaTracker.Worker — pre-existing, build succeeds with 0 errors, safe to ignore until NuGet cache refreshes
- 02-04: HardRock fallback bookmaker key 'hardrockbet' LOW confidence — validate against debug logs on first real run
- 03-01: pgdata Docker volume retains old user passwords across container recreations — if password changes, run ALTER USER via docker compose exec to sync

## Session Continuity

Last session: 2026-03-06
Stopped at: 04-04-PLAN.md Task 2 (checkpoint:human-verify) — Task 1 complete, awaiting visual verification of all Phase 4 success criteria at http://localhost:5173.
Resume signal: Type "approved" in next session after visual verification passes, or describe issues found.
