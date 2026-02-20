---
phase: 03-rest-api
plan: 01
subsystem: auth
tags: [jwt, bcrypt, aspnetcore, postgresql, minimal-api, refresh-tokens]

# Dependency graph
requires:
  - phase: 01-foundation
    provides: NbaTrackerDbContext, User entity, RefreshToken entity, EF migrations infrastructure
  - phase: 02-ingestion-worker
    provides: SyncRun entity for GET /api/admin/sync-status endpoint
provides:
  - POST /api/auth/login — email+BCrypt login, returns JWT access token (15 min) + refresh token (7 day)
  - POST /api/auth/refresh — token rotation, old refresh token revoked on use
  - POST /api/auth/logout — idempotent refresh token revocation
  - POST /api/admin/users — admin-only user creation with BCrypt hash
  - GET /api/admin/sync-status — admin-only last 10 SyncRun records
  - JWT bearer middleware with AdminOnly policy (ClaimTypes.Role == "Admin")
  - CORS for FrontendDev (localhost:3000/5173)
  - Admin seed from Seed__AdminEmail + Seed__AdminPassword env vars
  - Email column (unique index) added to Users table via AddEmailToUsers migration
affects: [04-frontend, deployment]

# Tech tracking
tech-stack:
  added:
    - Microsoft.AspNetCore.Authentication.JwtBearer 9.0.13
    - BCrypt.Net-Next 4.1.0
  patterns:
    - Minimal API route groups with RequireAuthorization("AdminOnly") gating entire group
    - BCrypt timing-safe verification (always hash even when user not found — prevents email enumeration)
    - Refresh token stored as BCrypt hash only — plaintext never persisted, returned to client once
    - Token rotation — old refresh token revoked before issuing new pair
    - ASP.NET Core env var to config key mapping: JWT__Secret env var -> JWT:Secret config key (via __ -> : translation)
    - Idempotent SQL script via dotnet ef migrations script --idempotent for local Docker PostgreSQL (same WSL2 NAT workaround as Phase 1)

key-files:
  created:
    - nba-lines-tracker/src/NbaTracker.Api/Services/TokenService.cs
    - nba-lines-tracker/src/NbaTracker.Api/Models/AuthModels.cs
    - nba-lines-tracker/src/NbaTracker.Api/Models/AdminModels.cs
    - nba-lines-tracker/src/NbaTracker.Api/Endpoints/AuthEndpoints.cs
    - nba-lines-tracker/src/NbaTracker.Api/Endpoints/AdminEndpoints.cs
    - nba-lines-tracker/src/NbaTracker.Data/Migrations/20260220005345_AddEmailToUsers.cs
  modified:
    - nba-lines-tracker/src/NbaTracker.Data/Entities/User.cs
    - nba-lines-tracker/src/NbaTracker.Data/NbaTrackerDbContext.cs
    - nba-lines-tracker/src/NbaTracker.Data/Migrations/NbaTrackerDbContextModelSnapshot.cs
    - nba-lines-tracker/src/NbaTracker.Api/NbaTracker.Api.csproj
    - nba-lines-tracker/src/NbaTracker.Api/Program.cs
    - nba-lines-tracker/.env.example

key-decisions:
  - "ASP.NET Core env var config key mapping: JWT__Secret env var becomes JWT:Secret config key via double-underscore-to-colon translation. Use config['JWT:Secret'] not config['JWT__Secret']"
  - "BCrypt timing-safe login: always call BCrypt.Verify even when user not found (with dummy hash) to prevent timing-based email enumeration"
  - "Refresh token BCrypt hash storage: plaintext returned to client once, TokenHash in DB stores BCrypt hash (cannot query by hash, so candidates fetched by time window and verified in memory)"
  - "Admin seed runs only in Development environment — requires ASPNETCORE_ENVIRONMENT=Development in .env for local Docker"
  - "Token rotation: old refresh token RevokedAt set before new token issued — prevents replay attacks"
  - "ClockSkew = TimeSpan.Zero: 15-minute access tokens expire in exactly 15 minutes, no default 5-minute leeway"

patterns-established:
  - "Route group auth gating: api.MapGroup('/admin').RequireAuthorization('AdminOnly') — no per-endpoint auth needed"
  - "Idempotent logout: always return 200, already-revoked token is not an error"
  - "Admin seed guard: AnyAsync(u => u.Email == adminEmail) prevents duplicate seeding on restarts"

requirements-completed: [AUTH-01, AUTH-02, AUTH-03, AUTH-04]

# Metrics
duration: 61min
completed: 2026-02-20
---

# Phase 3 Plan 01: Auth API Summary

**JWT bearer auth with BCrypt refresh token rotation: login/refresh/logout/create-user/sync-status endpoints behind AdminOnly policy, admin seeded from env vars**

## Performance

- **Duration:** 61 min
- **Started:** 2026-02-20T00:52:17Z
- **Completed:** 2026-02-20T01:53:50Z
- **Tasks:** 2
- **Files modified:** 11

## Accomplishments
- Email column added to Users table with unique index via EF migration, applied to Docker PostgreSQL via idempotent SQL script
- JWT bearer middleware with HS256, 15-min access tokens, ClockSkew=Zero; AdminOnly policy via ClaimTypes.Role=="Admin"
- Full auth flow: BCrypt timing-safe login, refresh token rotation (old revoked on use), idempotent logout, admin seed on startup from env vars
- Admin-only endpoints: user creation with duplicate-email guard (409), sync-status returning last 10 SyncRuns; regular users get 403

## Task Commits

Each task was committed atomically:

1. **Task 1: Email migration, User entity update, and package installation** - `3ce95ac` (feat)
2. **Task 2: TokenService, auth/admin endpoints, Program.cs wiring, and admin seed** - `a294ab1` (feat)

**Plan metadata:** (docs commit — created after state updates)

## Files Created/Modified
- `nba-lines-tracker/src/NbaTracker.Api/Services/TokenService.cs` - JWT access token generation (HS256), refresh token generation (64-byte random), BCrypt hash/verify
- `nba-lines-tracker/src/NbaTracker.Api/Endpoints/AuthEndpoints.cs` - POST /api/auth/login, POST /api/auth/refresh (rotation), POST /api/auth/logout (idempotent)
- `nba-lines-tracker/src/NbaTracker.Api/Endpoints/AdminEndpoints.cs` - POST /api/admin/users, GET /api/admin/sync-status
- `nba-lines-tracker/src/NbaTracker.Api/Models/AuthModels.cs` - LoginRequest, RefreshRequest, LoginResponse records
- `nba-lines-tracker/src/NbaTracker.Api/Models/AdminModels.cs` - CreateUserRequest record
- `nba-lines-tracker/src/NbaTracker.Api/Program.cs` - JWT bearer middleware, AddAuthorizationBuilder with AdminOnly policy, CORS FrontendDev, route groups, admin seed
- `nba-lines-tracker/src/NbaTracker.Data/Entities/User.cs` - Added Email property (required, unique)
- `nba-lines-tracker/src/NbaTracker.Data/NbaTrackerDbContext.cs` - Added HasIndex(u => u.Email).IsUnique()
- `nba-lines-tracker/src/NbaTracker.Data/Migrations/20260220005345_AddEmailToUsers.cs` - AddColumn Email + CreateIndex IX_Users_Email

## Decisions Made
- ASP.NET Core maps env var `JWT__Secret` to config key `JWT:Secret` — plan specified `config["JWT__Secret"]` but correct key is `config["JWT:Secret"]` (auto-fixed as deviation Rule 1)
- ASPNETCORE_ENVIRONMENT=Development added to .env so Docker API container runs the startup migration and seed code
- BCrypt timing-safe login: dummy hash computed when user not found to prevent email enumeration via timing attack
- Admin seed uses AnyAsync guard to be idempotent on every startup

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed ASP.NET Core config key for JWT__Secret env var**
- **Found during:** Task 2 (Program.cs and TokenService implementation)
- **Issue:** Plan specified `config["JWT__Secret"]` to read the JWT secret, but ASP.NET Core's environment variable configuration provider maps `JWT__Secret` env var to `JWT:Secret` config key (double underscore becomes colon separator). The API container failed at startup with `InvalidOperationException: JWT__Secret must be configured`.
- **Fix:** Changed `config["JWT__Secret"]` to `config["JWT:Secret"]` in TokenService.cs and Program.cs. Same pattern applied to `Seed__AdminEmail` -> `Seed:AdminEmail` and `Seed__AdminPassword` -> `Seed:AdminPassword`.
- **Files modified:** `nba-lines-tracker/src/NbaTracker.Api/Services/TokenService.cs`, `nba-lines-tracker/src/NbaTracker.Api/Program.cs`
- **Verification:** API container starts, "Seeded admin user: admin@nbatracker.local" logged; login endpoint returns 200 with tokens
- **Committed in:** `a294ab1` (Task 2 commit)

**2. [Rule 3 - Blocking] Added ASPNETCORE_ENVIRONMENT=Development to .env**
- **Found during:** Task 2 (verifying admin seed)
- **Issue:** API container ran in Production mode by default. The admin seed and OpenApi map were inside `if (app.Environment.IsDevelopment())` block. Without Development mode, admin was never seeded.
- **Fix:** Added `ASPNETCORE_ENVIRONMENT=Development` to local `.env` file (gitignored).
- **Files modified:** `.env` (local, gitignored)
- **Verification:** API logs show "Hosting environment: Development" and "Seeded admin user" on startup
- **Committed in:** Not committed separately (local .env is gitignored)

**3. [Rule 1 - Bug] Resolved Docker database authentication issue**
- **Found during:** Task 2 (end-to-end verification)
- **Issue:** The pgdata Docker volume retained old data from Phase 1 with a different password for the `dev` user. API container failed with `28P01 password authentication failed for user "dev"` on TCP connections (unix socket worked fine). The `docker compose up -d db` recreated the container but kept old volume data, so POSTGRES_USER init did not re-run.
- **Fix:** Used `docker compose exec db psql -U dev` (unix socket) to run `ALTER USER dev WITH PASSWORD 'devpassword'` to sync the password with the current .env file.
- **Files modified:** None (database operation)
- **Verification:** API container successfully ran MigrateAsync and connected to DB on next restart

---

**Total deviations:** 3 auto-fixed (2 Rule 1 bugs, 1 Rule 3 blocking)
**Impact on plan:** All auto-fixes required for correctness. The JWT/Seed config key translation is a fundamental ASP.NET Core behavior gap in the plan specification. Docker DB password sync was an environment state issue. No scope creep.

## Issues Encountered
- Docker Desktop was not in the shell PATH — found and used full path `C:/Program Files/Docker/Docker/resources/bin/docker.exe`
- Docker Desktop Linux engine pipe was initially not responding — launched Docker Desktop and confirmed engine was already running (default context issue)
- pgdata volume retained old DB state with wrong password — resolved via unix socket ALTER USER

## User Setup Required
None - no external service configuration required beyond the env vars already in .env.

## Next Phase Readiness
- Phase 4 (Frontend) has a working auth API: login/refresh/logout/create-user/sync-status all functional
- JWT issuer: `nbatracker-api`, audience: `nbatracker-client`, HMAC-SHA256, 15-min tokens
- CORS allows localhost:3000 and localhost:5173 with credentials
- Admin credentials set in local .env (Seed__AdminEmail, Seed__AdminPassword)
- Refresh tokens stored as BCrypt hashes in RefreshTokens table with 7-day expiry

## Self-Check: PASSED

All created files verified present on disk. All task commits (3ce95ac, a294ab1) verified in git log.

---
*Phase: 03-rest-api*
*Completed: 2026-02-20*
