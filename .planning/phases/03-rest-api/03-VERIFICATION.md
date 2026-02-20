---
phase: 03-rest-api
verified: 2026-02-19T00:00:00Z
status: human_needed
score: 12/12 must-haves verified
re_verification: false
human_verification:
  - test: "POST /api/auth/login with seeded admin credentials returns HTTP 200 with accessToken and refreshToken"
    expected: "JSON body contains accessToken (JWT string), refreshToken (base64 string), expiresIn: 900"
    why_human: "Runtime behavior — requires running Docker stack and seeded admin user"
  - test: "POST /api/auth/login with wrong credentials returns HTTP 401"
    expected: "HTTP 401 Unauthorized, no tokens issued"
    why_human: "Runtime behavior — BCrypt timing-safe verification path requires live server"
  - test: "POST /api/auth/refresh with a valid refresh token returns a new accessToken; same refresh token second time returns 401 (token rotation)"
    expected: "First call: 200 with new tokens. Second call with same refresh token: 401 (RevokedAt set)"
    why_human: "Token rotation side effect in database requires end-to-end execution"
  - test: "POST /api/auth/logout with a valid JWT sets RevokedAt in the database; logout is idempotent (returns 200 even for already-revoked token)"
    expected: "HTTP 200 on first call. HTTP 200 again on second call with same token. Refresh with that token after logout returns 401."
    why_human: "Database mutation and idempotency require live execution"
  - test: "POST /api/admin/users with admin JWT creates user (HTTP 201); same endpoint with non-admin JWT returns 403; unauthenticated returns 401"
    expected: "Admin JWT: 201 Created. Non-admin JWT: 403. No JWT: 401."
    why_human: "Authorization policy enforcement requires live JWT validation chain"
  - test: "GET /api/admin/sync-status with admin JWT returns HTTP 200 with array of up to 10 SyncRun objects"
    expected: "200 with JSON array. Each object has id, startedAt, completedAt, status, gamesProcessed, errorDetails."
    why_human: "Requires running stack with populated SyncRuns table"
  - test: "Admin seed runs on startup when Seed__AdminEmail and Seed__AdminPassword env vars are present (ASPNETCORE_ENVIRONMENT=Development)"
    expected: "On first startup: log line 'Seeded admin user: <email>'. On subsequent startups: seed is skipped (AnyAsync guard). Admin login works."
    why_human: "Requires Docker container startup and log inspection"
  - test: "GET /api/teams, GET /api/teams/{id}/stats, GET /api/teams/{id}/games all return correctly structured JSON with valid JWT; return 401 without JWT; return 404 for unknown id"
    expected: "/api/teams: 200 array of TeamStatsResponse. /api/teams/1/stats: 200 with home/away splits. /api/teams/1/games: 200 array sorted desc by date. Unknown id: 404. No JWT: 401."
    why_human: "Requires running Docker stack with Teams table populated by Phase 2 sync worker (or empty array if not yet synced)"
---

# Phase 3: REST API Verification Report

**Phase Goal:** Users and admins can authenticate, and all team stats endpoints return real data from the database
**Verified:** 2026-02-19
**Status:** human_needed (all static checks passed; runtime behavior requires human testing)
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | POST /api/auth/login with correct email+password returns HTTP 200 with accessToken and refreshToken | ? HUMAN | LoginAsync queries db.Users by Email, BCrypt-verifies password, returns `new LoginResponse(accessToken, refreshPlaintext, 900)` — logic correct, runtime unverified |
| 2 | POST /api/auth/login with wrong credentials returns HTTP 401 | ? HUMAN | `return Results.Unauthorized()` on null user or BCrypt failure — code correct |
| 3 | POST /api/auth/refresh with a valid refresh token returns a new accessToken (token rotation: old token revoked) | ? HUMAN | `match.RevokedAt = DateTime.UtcNow` before issuing new pair — rotation logic correct |
| 4 | POST /api/auth/logout with a valid JWT revokes the refresh token (RevokedAt set) | ? HUMAN | `match.RevokedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct)` — code correct; always returns 200 |
| 5 | POST /api/admin/users with admin JWT creates a new user; same endpoint returns 403 for non-admin JWTs | ? HUMAN | AdminEndpoints.Map wired to `RequireAuthorization("AdminOnly")` group in Program.cs — policy gates the group |
| 6 | GET /api/admin/sync-status returns last 10 SyncRun records for admin callers | ? HUMAN | `db.SyncRuns.OrderByDescending(r => r.StartedAt).Take(10).ToListAsync(ct)` — real DB query wired |
| 7 | Admin seed runs on startup when Seed__AdminEmail and Seed__AdminPassword env vars are present | ? HUMAN | Seed block present in Program.cs under `IsDevelopment()` guard with AnyAsync idempotency check |
| 8 | GET /api/teams returns HTTP 200 with array of TeamStatsResponse objects | ? HUMAN | TeamEndpoints.Map wired to `/api/teams` with `RequireAuthorization()` — bulk game query + C# partitioning correct |
| 9 | GET /api/teams/{id}/stats returns HTTP 200 with home/away ATS and O/U splits; 404 for unknown id | ? HUMAN | `db.Teams.FindAsync([id])` returns null → NotFound; otherwise two targeted queries + BuildSplit |
| 10 | GET /api/teams/{id}/games returns HTTP 200 with game log sorted descending by date; 404 for unknown id | ? HUMAN | `db.Teams.AnyAsync` null check, then games ordered descending by GameDate |
| 11 | All team endpoints return 401 when called without a JWT | ? HUMAN | `RequireAuthorization()` on `/teams` route group in Program.cs |
| 12 | Enum comparisons (AtsResult.Cover, OuResult.Over) happen in C# after ToListAsync — not inside EF LINQ | ✓ VERIFIED | `finalGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Cover)` — comparisons on in-memory List, not IQueryable |

**Score:** 12/12 truths structurally verified (static code analysis). 11/12 require runtime confirmation.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `nba-lines-tracker/src/NbaTracker.Api/Services/TokenService.cs` | JWT access token generation, refresh token generation, BCrypt hash/verify | ✓ VERIFIED | 55 lines; GenerateAccessToken, GenerateRefreshToken, HashRefreshToken, VerifyRefreshToken all implemented; ClaimTypes.Role set to "Admin"/"User" |
| `nba-lines-tracker/src/NbaTracker.Api/Endpoints/AuthEndpoints.cs` | POST /api/auth/login, POST /api/auth/refresh, POST /api/auth/logout | ✓ VERIFIED | 112 lines; all three endpoints implemented with full BCrypt verification, token rotation, idempotent logout |
| `nba-lines-tracker/src/NbaTracker.Api/Endpoints/AdminEndpoints.cs` | POST /api/admin/users, GET /api/admin/sync-status | ✓ VERIFIED | 57 lines; both endpoints implemented; sync-status queries db.SyncRuns with real DB call |
| `nba-lines-tracker/src/NbaTracker.Api/Endpoints/TeamEndpoints.cs` | GET /api/teams, GET /api/teams/{id}/stats, GET /api/teams/{id}/games | ✓ VERIFIED | 176 lines; all three endpoints fully implemented; BuildSplit helper; 404 guards on all detail endpoints |
| `nba-lines-tracker/src/NbaTracker.Api/Models/TeamModels.cs` | TeamStatsResponse, TeamDetailResponse, GameLogEntry DTOs | ✓ VERIFIED | All four records (TeamStatsResponse, HomeAwaySplit, TeamDetailResponse, GameLogEntry) with correct field shapes |
| `nba-lines-tracker/src/NbaTracker.Api/Models/AuthModels.cs` | LoginRequest, RefreshRequest, LoginResponse | ✓ VERIFIED | All three records present |
| `nba-lines-tracker/src/NbaTracker.Api/Models/AdminModels.cs` | CreateUserRequest | ✓ VERIFIED | Record with Email, Password, IsAdmin (default false) |
| `nba-lines-tracker/src/NbaTracker.Data/Entities/User.cs` | Email property on User entity | ✓ VERIFIED | `public string Email { get; set; } = null!;` present |
| `nba-lines-tracker/src/NbaTracker.Api/Program.cs` | JWT bearer middleware, AdminOnly policy, CORS, route groups, admin seed | ✓ VERIFIED | AddAuthentication, AddAuthorizationBuilder, AddCors, all three route groups, seed block all present |
| `nba-lines-tracker/src/NbaTracker.Api/NbaTracker.Api.csproj` | BCrypt.Net-Next and JwtBearer packages | ✓ VERIFIED | `BCrypt.Net-Next Version="4.*"` and `Microsoft.AspNetCore.Authentication.JwtBearer Version="9.*"` present |
| `nba-lines-tracker/src/NbaTracker.Data/Migrations/20260220005345_AddEmailToUsers.cs` | AddColumn Email + CreateIndex IX_Users_Email | ✓ VERIFIED | Migration contains AddColumn (nullable: false) and CreateIndex (unique: true) |
| `nba-lines-tracker/src/NbaTracker.Data/NbaTrackerDbContext.cs` | HasIndex(u => u.Email).IsUnique() | ✓ VERIFIED | Lines 73-75 contain the unique index configuration |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| AuthEndpoints.cs | NbaTrackerDbContext | `db.Users.FirstOrDefaultAsync(u => u.Email == req.Email, ct)` | ✓ WIRED | Line 24-25; exact pattern present |
| AuthEndpoints.cs | TokenService.cs | `tokens.GenerateAccessToken / tokens.VerifyRefreshToken / tokens.HashRefreshToken` | ✓ WIRED | All three method calls present across LoginAsync, RefreshAsync, LogoutAsync |
| Program.cs | AdminEndpoints.cs | `api.MapGroup("/admin").RequireAuthorization("AdminOnly")` | ✓ WIRED | Line 111; exact pattern confirmed |
| TokenService.cs | Program.cs JWT config | `ClaimTypes.Role` set to "Admin" maps to `RequireRole("Admin")` in AdminOnly policy | ✓ WIRED | TokenService line 28: `new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")`; Program.cs line 44: `.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))` |
| TeamEndpoints.cs | NbaTrackerDbContext | `db.Games.Where(g => g.Status == "FINAL").Include(g => g.GameResult).ToListAsync(ct)` | ✓ WIRED | Pattern confirmed at lines 26-29 |
| TeamEndpoints.cs | GameResult entity (AtsResult enum) | `g.GameResult?.HomeAtsResult == AtsResult.Cover` in C# after ToListAsync | ✓ WIRED | Enum comparisons at lines 43-48, 157-163 — all post-materialization |
| Program.cs | TeamEndpoints.cs | `TeamEndpoints.Map(api.MapGroup("/teams").RequireAuthorization())` | ✓ WIRED | Line 114 — confirmed |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| AUTH-01 | 03-01 | User can log in with email and password | ✓ SATISFIED | AuthEndpoints.LoginAsync: queries by Email, verifies BCrypt password, returns accessToken + refreshToken |
| AUTH-02 | 03-01, 03-02 | User session persists across browser refresh (JWT) | ✓ SATISFIED | Plan 03-01: JWT access tokens issued with 15-min expiry, refresh token rotation; Plan 03-02: team endpoints require JWT bearer (session proves persistence across requests) |
| AUTH-03 | 03-01 | User can log out from any page | ✓ SATISFIED | AuthEndpoints.LogoutAsync: sets RevokedAt on refresh token, always returns 200 (idempotent) |
| AUTH-04 | 03-01 | Admin can create user accounts (no public self-serve registration) | ✓ SATISFIED | AdminEndpoints.CreateUserAsync: behind `RequireAuthorization("AdminOnly")` — non-admin gets 403, unauthenticated gets 401 |

**Orphaned requirements check:** REQUIREMENTS.md maps only AUTH-01, AUTH-02, AUTH-03, AUTH-04 to Phase 3. All four are claimed in the plans. No orphaned requirements.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| None | - | - | - | No anti-patterns detected across all 9 created/modified source files |

Zero TODOs, FIXMEs, placeholders, empty return stubs, or console.log-only implementations found.

### Key Deviation: ASP.NET Core Environment Variable Config Key Translation

The plan specified `config["JWT__Secret"]` but ASP.NET Core's environment variable provider translates double underscores to colons: `JWT__Secret` env var becomes `JWT:Secret` config key. This was caught and auto-fixed during execution. The correct key `config["JWT:Secret"]` is used in both TokenService.cs (line 17) and Program.cs (line 23). Same translation applies to `Seed__AdminEmail` → `Seed:AdminEmail` and `Seed__AdminPassword` → `Seed:AdminPassword`.

### Human Verification Required

#### 1. Full Auth Flow: Login, Refresh, Logout

**Test:** With Docker stack running (`docker compose up -d`):
1. POST `http://localhost:5000/api/auth/login` with `{"email":"<Seed__AdminEmail>","password":"<Seed__AdminPassword>"}`
2. Capture `accessToken` and `refreshToken` from response

**Expected:** HTTP 200 with `{"accessToken":"eyJ...","refreshToken":"<base64>","expiresIn":900}`

**Why human:** Requires running Docker stack, seeded admin user, live JWT generation

#### 2. Wrong Credentials: 401

**Test:** POST `/api/auth/login` with incorrect password
**Expected:** HTTP 401, no tokens in body
**Why human:** Live BCrypt verification path

#### 3. Token Rotation

**Test:** POST `/api/auth/refresh` with the refresh token from step 1. Capture new tokens. POST `/api/auth/refresh` again with the SAME original refresh token.
**Expected:** First call: 200 with new access + refresh token pair. Second call: 401 (original token has RevokedAt set)
**Why human:** Database mutation side effect requires live execution

#### 4. Logout Revocation

**Test:** POST `/api/auth/logout` with a valid refresh token. Then attempt POST `/api/auth/refresh` with that same refresh token.
**Expected:** Logout: 200. Subsequent refresh: 401.
**Why human:** RevokedAt persistence requires live database

#### 5. AdminOnly Policy Enforcement

**Test:**
- GET `http://localhost:5000/api/admin/sync-status` without token → expect 401
- GET same endpoint with valid admin JWT → expect 200 with JSON array
- Create a regular user via `POST /api/admin/users` (IsAdmin: false), log in as them, attempt GET `/api/admin/sync-status` with their JWT → expect 403

**Why human:** JWT bearer middleware + authorization policy chain requires live ASP.NET Core runtime

#### 6. Team Endpoints Auth + Data

**Test:**
- GET `http://localhost:5000/api/teams` without token → expect 401
- GET same endpoint with valid JWT → expect 200 (array; may be empty if no sync run completed)
- GET `/api/teams/1/stats` with valid JWT → expect 200 with `{teamId, home, away}` structure
- GET `/api/teams/99999/stats` with valid JWT → expect 404

**Why human:** JWT bearer enforcement + EF Core query execution + PostgreSQL data requires live stack

#### 7. Admin Seed Idempotency

**Test:** Restart the Docker API container twice. Check logs for "Seeded admin user" message on first startup. Confirm no duplicate user error on second startup.
**Expected:** Admin user seeded once; subsequent restarts skip seeding (AnyAsync guard)
**Why human:** Requires container restart cycle and log inspection

### Gaps Summary

No structural gaps found. All artifacts exist, are substantive, and are correctly wired. The phase goal is achievable by the code as written. The remaining 11 human verification items are runtime behavioral checks that cannot be automated through static analysis — they require the Docker stack to be running with a seeded database.

---

_Verified: 2026-02-19_
_Verifier: Claude (gsd-verifier)_
