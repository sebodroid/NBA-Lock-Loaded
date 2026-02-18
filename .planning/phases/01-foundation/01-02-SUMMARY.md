---
phase: 01-foundation
plan: 02
subsystem: database
tags: [efcore, npgsql, postgres, migrations, entities, docker, aiven]

# Dependency graph
requires:
  - phase: 01-01
    provides: "NbaTrackerDbContext stub, EF Core + Npgsql packages, Docker Compose four-container environment"
provides:
  - "7 EF Core entity classes in NbaTracker.Data/Entities/ with correct schema"
  - "NbaTrackerDbContext with 7 DbSets, string-backed enum converters, and performance indexes"
  - "IDesignTimeDbContextFactory enabling dotnet ef commands against the class library"
  - "InitialCreate migration (20260218024436) applied to local PostgreSQL (7 tables)"
  - "InitialCreate migration applied to Aiven PostgreSQL (7 tables)"
  - "All four Docker containers building and running cleanly"
  - ".dockerignore excluding host bin/obj from Docker build context"
affects: [02-data-sync, 03-auth, 04-frontend]

# Tech tracking
tech-stack:
  added:
    - "EF Core migrations (dotnet ef migrations add, database update)"
    - "Aiven PostgreSQL cloud database (SSL Mode=Require;Trust Server Certificate=true)"
    - ".dockerignore for multi-project .NET Docker builds"
  patterns:
    - "Apply migrations via idempotent SQL script (docker compose cp + psql exec) when dotnet ef TCP auth fails on Windows/WSL2"
    - "Use --connection flag on dotnet ef database update for cloud databases with custom SSL settings"
    - "DesignTimeDbContextFactory reads ConnectionStrings__Default env var with hardcoded local fallback"
    - "ATS/O/U results stored as string-backed enums: Cover/Loss/Push, Over/Under/Push — never boolean"
    - "FavoriteTeamId is explicit FK column in GameLines — never inferred from spread sign"
    - "Enums stored as text columns via HasConversion<string>() in OnModelCreating"
    - "Game has two FKs to Team configured with OnDelete(DeleteBehavior.Restrict) to avoid EF cascade ambiguity"

key-files:
  created:
    - nba-lines-tracker/src/NbaTracker.Data/Entities/Team.cs
    - nba-lines-tracker/src/NbaTracker.Data/Entities/Game.cs
    - nba-lines-tracker/src/NbaTracker.Data/Entities/GameLine.cs
    - nba-lines-tracker/src/NbaTracker.Data/Entities/GameResult.cs
    - nba-lines-tracker/src/NbaTracker.Data/Entities/User.cs
    - nba-lines-tracker/src/NbaTracker.Data/Entities/RefreshToken.cs
    - nba-lines-tracker/src/NbaTracker.Data/Entities/SyncRun.cs
    - nba-lines-tracker/src/NbaTracker.Data/Migrations/20260218024436_InitialCreate.cs
    - nba-lines-tracker/src/NbaTracker.Data/Migrations/20260218024436_InitialCreate.Designer.cs
    - nba-lines-tracker/src/NbaTracker.Data/Migrations/NbaTrackerDbContextModelSnapshot.cs
    - nba-lines-tracker/.dockerignore
  modified:
    - nba-lines-tracker/src/NbaTracker.Data/NbaTrackerDbContext.cs
    - nba-lines-tracker/src/NbaTracker.Data/DesignTimeDbContextFactory.cs
    - nba-lines-tracker/src/NbaTracker.Api/NbaTracker.Api.csproj

key-decisions:
  - "ATS/O/U enums as 3-value Cover/Loss/Push, Over/Under/Push stored as text — boolean would corrupt push-case analytics"
  - "FavoriteTeamId explicit FK in GameLines — spread sign alone cannot determine favorite (same absolute value for both sides)"
  - "Game.WentToOvertime as nullable bool — required for O/U push detection (OT game with total hitting the line exactly is a push)"
  - "TokenHash in RefreshToken stores BCrypt hash of token, not plaintext — security requirement"
  - "All enum columns stored as text via HasConversion<string>() — readability in DB and avoids integer-to-enum mapping bugs"
  - ".dockerignore added as bug fix — host obj/project.assets.json contained Windows Visual Studio fallback folder paths breaking Linux Docker builds"
  - "Migrations applied to local PostgreSQL via idempotent SQL script inside Docker container — Windows/WSL2 Docker Desktop routes localhost:5432 traffic through NAT, bypassing pg_hba.conf trust rules, causing SCRAM-SHA-256 auth failure even with correct password"
  - "Migrations applied to Aiven via dotnet ef database update --connection flag — standard TCP SSL connection works for cloud PostgreSQL"

patterns-established:
  - ".dockerignore at solution root: always exclude **/bin/ and **/obj/ to prevent Windows host NuGet cache from corrupting Linux Docker builds"
  - "SQL script migration pattern: dotnet ef migrations script --idempotent + docker compose cp + psql exec — works when TCP auth is unavailable"
  - "Aiven connection pattern: SSL Mode=Require;Trust Server Certificate=true in Npgsql connection string"

requirements-completed: []

# Metrics
duration: 30min
completed: 2026-02-18
---

# Phase 1 Plan 02: Entity Schema, Migrations, and Database Initialization Summary

**7-table EF Core schema with string-backed enums and explicit FKs, InitialCreate migration applied to local PostgreSQL (Docker) and Aiven cloud PostgreSQL, all four containers running cleanly**

## Performance

- **Duration:** ~30 min
- **Started:** 2026-02-18T18:05:19Z
- **Completed:** 2026-02-18T18:35:00Z
- **Tasks:** 2
- **Files modified:** 15

## Accomplishments

- Seven entity classes authored with correct schema: NbaApiId external ID, explicit FavoriteTeamId FK in GameLines, 3-value ATS/O/U enums (Cover/Loss/Push, Over/Under/Push), TokenHash (not plaintext) in RefreshTokens, WentToOvertime flag for O/U push detection
- NbaTrackerDbContext replaced the stub with 7 DbSets, string enum converters, dual-FK Game->Team configuration (Restrict on delete), FavoriteTeamId optional FK, and all performance indexes (NbaGameId unique, Season+Status composite, NbaApiId unique, TokenHash unique)
- InitialCreate migration applied to local PostgreSQL (Docker container) producing 8 tables (7 entity + __EFMigrationsHistory), verified via psql \dt
- InitialCreate migration applied to Aiven PostgreSQL via dotnet ef database update --connection with SSL, verified via psql \dt showing same 8 tables
- All four Docker containers (db, api, worker, frontend) building and running cleanly; /health returns HTTP 200 {"status":"healthy"}

## Task Commits

Each task was committed atomically:

1. **Task 1: Entity classes, DbContext, DesignTimeDbContextFactory** - `0c9b974` (feat) — prior session
2. **Task 2a: Generate InitialCreate migration + commit project config files** - `0afeebe` (feat)
3. **Task 2b: Fix Docker build with .dockerignore** - `2b76d6f` (fix)

**Plan metadata:** (this commit)

## Files Created/Modified

- `nba-lines-tracker/src/NbaTracker.Data/Entities/Team.cs` - Team with NbaApiId external ID, HomeGames/AwayGames collections
- `nba-lines-tracker/src/NbaTracker.Data/Entities/Game.cs` - Game with NbaGameId + OddsApiGameId external IDs, WentToOvertime bool
- `nba-lines-tracker/src/NbaTracker.Data/Entities/GameLine.cs` - GameLine with explicit FavoriteTeamId FK, Bookmaker field, Spread/Total/odds columns
- `nba-lines-tracker/src/NbaTracker.Data/Entities/GameResult.cs` - GameResult with nullable AtsResult/OuResult string-backed enums
- `nba-lines-tracker/src/NbaTracker.Data/Entities/User.cs` - User with PasswordHash (BCrypt), IsAdmin flag
- `nba-lines-tracker/src/NbaTracker.Data/Entities/RefreshToken.cs` - RefreshToken with TokenHash (not plaintext), RevokedAt nullable
- `nba-lines-tracker/src/NbaTracker.Data/Entities/SyncRun.cs` - SyncRun with SyncRunStatus enum, ErrorDetails JSON column
- `nba-lines-tracker/src/NbaTracker.Data/NbaTrackerDbContext.cs` - Full DbContext replacing stub: 7 DbSets, enum converters, FK config, indexes
- `nba-lines-tracker/src/NbaTracker.Data/DesignTimeDbContextFactory.cs` - IDesignTimeDbContextFactory with env var + localhost fallback
- `nba-lines-tracker/src/NbaTracker.Data/Migrations/20260218024436_InitialCreate.cs` - Full migration: 7 tables, all indexes
- `nba-lines-tracker/src/NbaTracker.Data/Migrations/20260218024436_InitialCreate.Designer.cs` - EF Core snapshot metadata
- `nba-lines-tracker/src/NbaTracker.Data/Migrations/NbaTrackerDbContextModelSnapshot.cs` - Current schema snapshot
- `nba-lines-tracker/.dockerignore` - Excludes bin/, obj/, node_modules/, .env from Docker build context
- `nba-lines-tracker/src/NbaTracker.Api/NbaTracker.Api.csproj` - Added Microsoft.EntityFrameworkCore.Design for dotnet ef tooling
- `nba-lines-tracker/src/NbaTracker.Api/Properties/launchSettings.json` - Standard VS launch profiles
- `nba-lines-tracker/src/NbaTracker.Worker/Properties/launchSettings.json` - Standard VS launch profiles

## Decisions Made

- **ATS/O/U as 3-value enums** — `Cover/Loss/Push` and `Over/Under/Push` stored as text strings. Boolean would make push results unrepresentable, corrupting analytics for games where the final score exactly hits the spread or total.
- **FavoriteTeamId explicit FK** — Spread is stored as an absolute value from the favorite's perspective. Without a separate FK column, there's no way to determine which side is the favorite — the same spread value appears on both teams' records from The Odds API.
- **Game.WentToOvertime nullable bool** — Required for O/U push detection: if a game goes to OT and the combined score exactly equals the total, it should be a push, not an over. Nullable because the value is unknown until the game is final.
- **TokenHash, not plaintext token** — RefreshTokens stores the BCrypt hash of the token. Plaintext would mean a database breach exposes all active sessions.
- **.dockerignore as bug fix** — Windows host `obj/project.assets.json` files contain references to `C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages` which don't exist in the Linux Docker container. Without `.dockerignore`, `dotnet restore` inside Docker cached the Windows paths, then `dotnet publish --no-restore` crashed with NuGet.Packaging.Core.PackagingException.
- **Local migration via SQL script** — Docker Desktop on Windows routes `localhost:5432` connections through WSL2 NAT, so the source IP arriving at PostgreSQL is not `127.0.0.1`. The `pg_hba.conf` `trust` rule for `127.0.0.1/32` is bypassed and SCRAM-SHA-256 is required — but the SCRAM auth fails even with the correct password. Workaround: generate idempotent SQL script, copy into container, run via `docker compose exec psql` (Unix socket = trust auth).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Added .dockerignore to prevent Windows NuGet paths from corrupting Docker build**
- **Found during:** Task 2 (docker compose build api)
- **Issue:** `dotnet publish --no-restore` inside Docker failed with `NuGet.Packaging.Core.PackagingException: Unable to find fallback package folder 'C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages'`. The Windows host's `obj/` directories were included in the Docker build context. The `project.assets.json` from the host machine cached Windows-specific NuGet fallback paths which don't exist in Linux.
- **Fix:** Created `.dockerignore` at the solution root excluding `**/bin/`, `**/obj/`, `**/node_modules/`, `frontend/dist/`, and `.env`.
- **Files modified:** `nba-lines-tracker/.dockerignore` (new file)
- **Verification:** `docker compose build api` and `docker compose build worker` both succeed. All four containers start and run.
- **Committed in:** `2b76d6f` (fix commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Bug fix was required for Docker builds to work. No scope creep — `.dockerignore` is the standard fix for this class of Windows-to-Linux Docker build issues.

## Issues Encountered

- **dotnet ef database update TCP auth failure on Windows/WSL2** — The `dotnet ef database update` command cannot connect to the local Docker PostgreSQL container from the Windows host via `localhost:5432`. The connection routes through Docker Desktop's WSL2 NAT, arriving at PostgreSQL as a non-localhost IP, bypassing the `pg_hba.conf` trust rule for `127.0.0.1/32`. SCRAM-SHA-256 authentication is required but fails even with the correct password (`devpassword`). Workaround: used `dotnet ef migrations script --idempotent` to generate SQL, then `docker compose cp` + `docker compose exec psql` to apply it inside the container via Unix socket (trust auth). This workaround is documented in the patterns section. Note: `dotnet ef database update --connection` works correctly for Aiven because Aiven uses standard TCP SSL auth that Npgsql handles properly.

## Phase 1 Success Criteria Verification

All four Phase 1 criteria verified:

1. **Criterion 1 — All four containers running:** PASS
   - `db`: Up, healthy, port 5432 exposed
   - `api`: Up, port 5000->8080, `/health` returns HTTP 200 `{"status":"healthy"}`
   - `worker`: Up (no exposed port, background service)
   - `frontend`: Up, port 3000->80

2. **Criterion 2 — EF Core migrations applied to both databases:** PASS
   - Local: 8 tables in nbatracker DB (Teams, Games, GameLines, GameResults, Users, RefreshTokens, SyncRuns, __EFMigrationsHistory)
   - Aiven: Same 8 tables confirmed via `psql \dt` through Docker psql container

3. **Criterion 3 — NbaTracker.Data referenced by API and Worker, solution builds:** PASS
   - `dotnet build NbaTracker.sln` — Build succeeded, 0 errors, all three projects listed

4. **Criterion 4 — Secrets not committed:** PASS
   - `.env` not in git status
   - `git log --all --full-history -- .env` — no commits containing `.env`
   - Aiven connection string was used only in CLI command, never written to any file

## User Setup Required

None — no external service configuration required beyond what's already in `.env`. The Aiven connection string used for migration is not stored in any committed file. When Phase 5 adds Aiven as the production database, the connection string will be added to `.env` (gitignored) under `ConnectionStrings__Aiven`.

## Next Phase Readiness

- Phase 1 complete: all four containers running, schema applied to both local and Aiven PostgreSQL
- Ready for Phase 2: data sync — BallDontLie API client for games, The Odds API client for betting lines, Worker Service sync logic
- No blockers for Phase 2
- Note: local PostgreSQL migrations should be re-applied if the Docker volume is reset (`docker compose down -v` destroys the volume). Run the SQL script approach again: generate migration.sql, docker compose cp, psql exec.

---
*Phase: 01-foundation*
*Completed: 2026-02-18*

## Self-Check: PASSED

- All 12 key files verified present on disk
- Commits 0c9b974 (Task 1), 0afeebe (Task 2a), 2b76d6f (Task 2b) verified in git log
- dotnet build NbaTracker.sln: Build succeeded, 0 errors
- Local PostgreSQL: 8 tables confirmed via docker compose exec psql
- Aiven PostgreSQL: 8 tables confirmed via docker run psql
- /health returns HTTP 200 {"status":"healthy"}
- .env not visible in git status (gitignored)
- Aiven credentials not committed to any file
