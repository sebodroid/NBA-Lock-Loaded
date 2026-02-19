# Phase 3: REST API - Research

**Researched:** 2026-02-19
**Domain:** ASP.NET Core 9 Minimal API, JWT authentication with refresh tokens, role-based authorization, EF Core aggregate queries for team stats
**Confidence:** HIGH

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| AUTH-01 | User can log in with email and password | `POST /api/auth/login` endpoint; BCrypt.Verify against stored PasswordHash; returns signed JWT + plaintext refresh token |
| AUTH-02 | User session persists across browser refresh (JWT) | Short-lived access token (15min) stored in memory; long-lived refresh token (7 days) in HttpOnly cookie or localStorage; `/api/auth/refresh` endpoint issues new access token |
| AUTH-03 | User can log out from any page | `POST /api/auth/logout` revokes the current refresh token by setting `RevokedAt` in RefreshTokens table |
| AUTH-04 | Admin can create user accounts (no public self-serve registration) | `POST /api/admin/users` endpoint behind `RequireAuthorization("AdminOnly")` policy; BCrypt.HashPassword for new user; no public registration route |
</phase_requirements>

---

## Summary

Phase 3 wires up the ASP.NET Core Minimal API project that was stubbed in Phase 1 with real authentication and data endpoints. The API project already exists at `nba-lines-tracker/src/NbaTracker.Api/` with a bare `Program.cs` that only migrates the DB and exposes `/health`. Phase 3 fills it with two plans: (1) JWT auth endpoints and admin user creation, and (2) team stats query endpoints.

The standard stack for this project is already decided: `Microsoft.AspNetCore.Authentication.JwtBearer` for JWT Bearer middleware, `BCrypt.Net-Next` for password hashing, `System.IdentityModel.Tokens.Jwt` for token generation, and EF Core against the existing `NbaTrackerDbContext`. No ASP.NET Core Identity framework — that is overkill for this small friend-group app. The `User` entity stores `PasswordHash` (BCrypt) and `IsAdmin` (bool). The `RefreshToken` entity stores `TokenHash` (BCrypt hash of the plaintext token), `ExpiresAt`, and nullable `RevokedAt`.

A schema issue requires a migration before auth logic can be written: the `Users` table has a `Username` column but AUTH-01 requires login by **email**. The `User` entity must gain an `Email` column (and ideally a unique index). This migration is the first task of plan 03-01. The team stats endpoints (plan 03-02) require EF Core LEFT JOINs from Teams through Games/GameResults to compute aggregate counts (Cover/Loss/Push per team, wins/losses, etc.) — these translate cleanly to SQL when written as `Count(g => g.HomeAtsResult == AtsResult.Cover)` style projections after a join, rather than GroupBy.

**Primary recommendation:** Add the Email column migration first. Use `MapGroup` + static endpoint classes to keep `Program.cs` clean. Generate JWT with `JwtSecurityTokenHandler` + `SecurityTokenDescriptor`. Store plaintext refresh token in response body; store its BCrypt hash in `RefreshTokens`. For team stats, query with EF Core LEFT JOINs and compute aggregates in a single LINQ projection — do not use in-memory GroupBy.

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 9.* | JWT Bearer middleware; validates Authorization header tokens | First-party; the standard for JWT-protected Minimal APIs in .NET 9 |
| `System.IdentityModel.Tokens.Jwt` | 8.* | `JwtSecurityTokenHandler` + `SecurityTokenDescriptor` for token generation | Standard .NET JWT library; used by the JwtBearer middleware internally |
| `BCrypt.Net-Next` | 4.* | `BCrypt.HashPassword` / `BCrypt.Verify` for passwords and refresh token hashes | Already chosen in Phase 1 research; STACK.md and prior decisions locked this in |
| `Microsoft.EntityFrameworkCore` | 9.* | Data access via existing `NbaTrackerDbContext` | Already in NbaTracker.Api.csproj |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 9.* | PostgreSQL driver | Already in NbaTracker.Api.csproj |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.AspNetCore.OpenApi` | 9.* | OpenAPI spec generation | Already in Api.csproj; enables Swagger UI for manual testing |
| `System.Security.Claims` | built-in | `ClaimTypes.Sub`, `ClaimTypes.Role`, `ClaimTypes.Email` for JWT payload | Needed in token generation; no extra package |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Custom JWT generation via `JwtSecurityTokenHandler` | ASP.NET Core Identity | Identity adds EF tables, Razor Pages, PasswordHasher infrastructure — overkill for 5 users |
| BCrypt for refresh token hash | SHA-256 HMAC | BCrypt is slower (intentional for passwords); SHA-256 HMAC is faster and appropriate for random tokens, but BCrypt is simpler to implement consistently and the performance is irrelevant at this scale |
| Storing refresh token as BCrypt hash | Storing plaintext in DB | Decision locked: `TokenHash` column stores BCrypt hash, never plaintext (prior decision 01-02) |
| Role stored as `IsAdmin` bool | String role column | `IsAdmin: bool` is already in the migration — use it to derive the role claim in JWT generation |

**Installation (packages not yet in Api.csproj):**
```bash
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 9.*
dotnet add package BCrypt.Net-Next --version 4.*
# System.IdentityModel.Tokens.Jwt is a transitive dependency of JwtBearer — no explicit add needed
```

---

## Architecture Patterns

### Recommended Project Structure

```
NbaTracker.Api/
├── Program.cs                     # DI registration, middleware, MapGroup calls
├── Endpoints/
│   ├── AuthEndpoints.cs           # POST /api/auth/login, /api/auth/refresh, /api/auth/logout
│   ├── AdminEndpoints.cs          # POST /api/admin/users, GET /api/admin/sync-status
│   └── TeamEndpoints.cs           # GET /api/teams, /api/teams/{id}/stats, /api/teams/{id}/games
├── Services/
│   └── TokenService.cs            # GenerateAccessToken(), GenerateRefreshToken(), HashToken(), VerifyToken()
└── Models/                        # Request/response DTOs (not EF entities)
    ├── LoginRequest.cs
    ├── LoginResponse.cs
    ├── CreateUserRequest.cs
    ├── TeamStatsResponse.cs
    └── GameLogResponse.cs
```

### Pattern 1: JWT Bearer Setup in Program.cs

**What:** Register AddAuthentication + AddJwtBearer with explicit TokenValidationParameters. In .NET 9, `WebApplication` auto-registers the middleware when services are registered — but explicit `UseAuthentication` / `UseAuthorization` calls are required to control ordering (especially with CORS).

**Source:** https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/security?view=aspnetcore-9.0

```csharp
// Program.cs
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var jwtSecret = builder.Configuration["Jwt__Secret"]
    ?? throw new InvalidOperationException("Jwt__Secret must be set");
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "nbatracker-api",
            ValidateAudience = true,
            ValidAudience = "nbatracker-client",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,           // no leeway on expiry
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

// CORS must go before UseAuthentication to keep ordering correct
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

app.UseCors("FrontendDev");
app.UseAuthentication();
app.UseAuthorization();
```

### Pattern 2: MapGroup for Endpoint Organization

**What:** Use `MapGroup` with a common prefix and apply authorization to the entire group. Split auth, admin, and team endpoints into separate static classes with a `Map(RouteGroupBuilder group)` pattern.

**Source:** https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/route-handlers?view=aspnetcore-9.0

```csharp
// Program.cs — after app.UseAuthorization()
var api = app.MapGroup("/api");

// Public auth endpoints — no RequireAuthorization()
AuthEndpoints.Map(api.MapGroup("/auth"));

// Admin endpoints — require "AdminOnly" policy
AdminEndpoints.Map(api.MapGroup("/admin").RequireAuthorization("AdminOnly"));

// Team endpoints — any authenticated user
TeamEndpoints.Map(api.MapGroup("/teams").RequireAuthorization());
```

```csharp
// Endpoints/AuthEndpoints.cs
public static class AuthEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest req,
        NbaTrackerDbContext db,
        TokenService tokens,
        CancellationToken ct)
    {
        // Implementation — see Code Examples
    }
    // ...
}
```

### Pattern 3: JWT Token Generation

**What:** Use `JwtSecurityTokenHandler` + `SecurityTokenDescriptor` to mint access tokens. The role claim must use `ClaimTypes.Role` so ASP.NET Core's role middleware recognises it.

```csharp
// Services/TokenService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

public class TokenService
{
    private readonly SymmetricSecurityKey _key;

    public TokenService(IConfiguration config)
    {
        var secret = config["Jwt__Secret"]
            ?? throw new InvalidOperationException("Jwt__Secret required");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    public string GenerateAccessToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "nbatracker-api",
            Audience = "nbatracker-client",
            SigningCredentials = new SigningCredentials(
                _key, SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }

    // Generates a cryptographically random refresh token (plaintext)
    public string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    // Stores BCrypt hash of refresh token in DB — never the plaintext
    public string HashRefreshToken(string plaintext)
        => BCrypt.Net.BCrypt.HashPassword(plaintext);

    // Verifies incoming plaintext token against stored hash
    public bool VerifyRefreshToken(string plaintext, string hash)
        => BCrypt.Net.BCrypt.Verify(plaintext, hash);
}
```

### Pattern 4: Login Endpoint

```csharp
private static async Task<IResult> LoginAsync(
    LoginRequest req,
    NbaTrackerDbContext db,
    TokenService tokens,
    CancellationToken ct)
{
    var user = await db.Users
        .Include(u => u.RefreshTokens)
        .FirstOrDefaultAsync(u => u.Email == req.Email, ct);

    // Constant-time: always call BCrypt.Verify even if user not found
    // (prevents timing-based email enumeration)
    var hashToCheck = user?.PasswordHash ?? BCrypt.Net.BCrypt.HashPassword("dummy");
    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, hashToCheck))
        return Results.Unauthorized();

    var accessToken = tokens.GenerateAccessToken(user);
    var refreshPlaintext = tokens.GenerateRefreshToken();

    db.RefreshTokens.Add(new RefreshToken
    {
        UserId = user.Id,
        TokenHash = tokens.HashRefreshToken(refreshPlaintext),
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        CreatedAt = DateTime.UtcNow
    });

    user.LastLoginAt = DateTime.UtcNow;
    await db.SaveChangesAsync(ct);

    return Results.Ok(new LoginResponse
    {
        AccessToken = accessToken,
        RefreshToken = refreshPlaintext,     // plaintext goes to client
        ExpiresIn = 900                      // 15 minutes in seconds
    });
}
```

### Pattern 5: Refresh Token Endpoint

```csharp
private static async Task<IResult> RefreshAsync(
    RefreshRequest req,
    NbaTrackerDbContext db,
    TokenService tokens,
    CancellationToken ct)
{
    // Find all non-expired, non-revoked tokens and check against incoming
    // Cannot query by hash directly — BCrypt is not reversible
    // Strategy: fetch candidate tokens for a time window and verify
    // PERFORMANCE NOTE: this requires checking recent tokens in memory.
    // Scope query to tokens created in the last 7 days to limit row count.
    var candidates = await db.RefreshTokens
        .Include(rt => rt.User)
        .Where(rt => rt.ExpiresAt > DateTime.UtcNow
                  && rt.RevokedAt == null
                  && rt.CreatedAt > DateTime.UtcNow.AddDays(-8))
        .ToListAsync(ct);

    var match = candidates.FirstOrDefault(rt =>
        tokens.VerifyRefreshToken(req.RefreshToken, rt.TokenHash));

    if (match is null)
        return Results.Unauthorized();

    // Rotate: revoke old token, issue new pair
    match.RevokedAt = DateTime.UtcNow;

    var newAccessToken = tokens.GenerateAccessToken(match.User);
    var newRefreshPlaintext = tokens.GenerateRefreshToken();

    db.RefreshTokens.Add(new RefreshToken
    {
        UserId = match.UserId,
        TokenHash = tokens.HashRefreshToken(newRefreshPlaintext),
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        CreatedAt = DateTime.UtcNow
    });

    await db.SaveChangesAsync(ct);

    return Results.Ok(new LoginResponse
    {
        AccessToken = newAccessToken,
        RefreshToken = newRefreshPlaintext,
        ExpiresIn = 900
    });
}
```

### Pattern 6: Admin User Creation

```csharp
// POST /api/admin/users — only reachable if AdminOnly policy passes
private static async Task<IResult> CreateUserAsync(
    CreateUserRequest req,
    NbaTrackerDbContext db,
    CancellationToken ct)
{
    var exists = await db.Users.AnyAsync(u => u.Email == req.Email, ct);
    if (exists)
        return Results.Conflict(new { error = "Email already registered" });

    db.Users.Add(new User
    {
        Email = req.Email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
        IsAdmin = req.IsAdmin,
        CreatedAt = DateTime.UtcNow
    });

    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/admin/users", new { email = req.Email });
}
```

### Pattern 7: Team Stats EF Core Query

**What:** Compute per-team aggregate stats (wins, losses, ATS cover/loss/push counts, O/U over/under/push counts) in a single LEFT JOIN query from Teams through Games/GameResults.

**Key insight:** EF Core can translate conditional count patterns like `games.Count(g => g.HomeAtsResult == AtsResult.Cover)` to SQL only when applied after a navigation-property join. Use LEFT JOIN with `DefaultIfEmpty()` or include navigation properties.

**Source:** https://learn.microsoft.com/en-us/ef/core/querying/complex-query-operators

```csharp
// GET /api/teams — returns all 30 teams with aggregate stats
private static async Task<IResult> GetTeamsAsync(
    NbaTrackerDbContext db,
    CancellationToken ct)
{
    var teams = await db.Teams.ToListAsync(ct);

    // For each team, compute stats in a single query per team or as a batch.
    // Batch approach: load all FINAL games with GameResults in one query,
    // then compute per-team aggregates in memory (30 teams × ~82 games is tiny).

    var finalGames = await db.Games
        .Where(g => g.Status == "FINAL")
        .Include(g => g.GameResult)
        .Include(g => g.HomeTeam)
        .Include(g => g.AwayTeam)
        .ToListAsync(ct);

    var stats = teams.Select(team =>
    {
        var homeGames = finalGames.Where(g => g.HomeTeamId == team.Id).ToList();
        var awayGames = finalGames.Where(g => g.AwayTeamId == team.Id).ToList();
        var allGames = homeGames.Concat(awayGames).ToList();

        return new TeamStatsResponse
        {
            TeamId = team.Id,
            Name = team.Name,
            Abbreviation = team.Abbreviation,
            Conference = team.Conference,
            Division = team.Division,
            Wins = homeGames.Count(g => g.HomeScore > g.AwayScore)
                 + awayGames.Count(g => g.AwayScore > g.HomeScore),
            Losses = homeGames.Count(g => g.HomeScore < g.AwayScore)
                   + awayGames.Count(g => g.AwayScore < g.HomeScore),
            AtsCovers = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Cover)
                      + awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Cover),
            AtsLosses = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Loss)
                      + awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Loss),
            AtsPushes = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Push)
                      + awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Push),
            OuOvers = allGames.Count(g => g.GameResult?.OuResult == OuResult.Over),
            OuUnders = allGames.Count(g => g.GameResult?.OuResult == OuResult.Under),
            OuPushes = allGames.Count(g => g.GameResult?.OuResult == OuResult.Push),
            GamesPlayed = allGames.Count
        };
    }).ToList();

    return Results.Ok(stats);
}
```

**Note on scale:** 30 teams × ~82 games = ~2,460 rows. Loading all FINAL games with includes in one DB query and computing aggregates in memory is acceptable at this scale. A full-season's data is under 100KB of memory. This avoids complex EF Core GroupBy translation issues.

### Pattern 8: Team Game Log Query

```csharp
// GET /api/teams/{id}/games — per-team game-by-game log
private static async Task<IResult> GetTeamGamesAsync(
    int id,
    NbaTrackerDbContext db,
    CancellationToken ct)
{
    var team = await db.Teams.FindAsync([id], ct);
    if (team is null) return Results.NotFound();

    var games = await db.Games
        .Where(g => (g.HomeTeamId == id || g.AwayTeamId == id)
                 && g.Status == "FINAL")
        .Include(g => g.HomeTeam)
        .Include(g => g.AwayTeam)
        .Include(g => g.GameLine)
        .Include(g => g.GameResult)
        .OrderByDescending(g => g.GameDate)
        .Select(g => new GameLogResponse
        {
            GameId = g.Id,
            GameDate = g.GameDate,
            HomeTeamAbbr = g.HomeTeam.Abbreviation,
            AwayTeamAbbr = g.AwayTeam.Abbreviation,
            HomeScore = g.HomeScore,
            AwayScore = g.AwayScore,
            IsHomeGame = g.HomeTeamId == id,
            SpreadLine = g.GameLine != null ? g.GameLine.Spread : null,
            TotalLine = g.GameLine != null ? g.GameLine.Total : null,
            AtsResult = g.HomeTeamId == id
                ? g.GameResult != null ? g.GameResult.HomeAtsResult : null
                : g.GameResult != null ? g.GameResult.AwayAtsResult : null,
            OuResult = g.GameResult != null ? g.GameResult.OuResult : null
        })
        .ToListAsync(ct);

    return Results.Ok(games);
}
```

### Pattern 9: Sync Status Admin Endpoint

This endpoint was deferred in Phase 2 (see 02-CONTEXT.md deferred items) and belongs to Phase 3:

```csharp
// GET /api/admin/sync-status — behind AdminOnly policy
private static async Task<IResult> GetSyncStatusAsync(
    NbaTrackerDbContext db,
    CancellationToken ct)
{
    var recent = await db.SyncRuns
        .OrderByDescending(r => r.StartedAt)
        .Take(10)
        .ToListAsync(ct);

    return Results.Ok(recent.Select(r => new
    {
        r.Id,
        r.StartedAt,
        r.CompletedAt,
        Status = r.Status.ToString(),
        r.GamesProcessed,
        r.ErrorDetails
    }));
}
```

### Anti-Patterns to Avoid

- **Storing plaintext refresh tokens in the DB:** The `TokenHash` column stores BCrypt hash only. The plaintext token is returned to the client exactly once and never persisted. (Prior decision 01-02 is locked.)
- **Using `IsAdmin` bool as the role string directly:** The JWT must include `ClaimTypes.Role` with the string `"Admin"` or `"User"` — not the bool. ASP.NET Core's `RequireRole("Admin")` checks the `ClaimTypes.Role` claim value.
- **Combining `AllowAnyOrigin()` with `AllowCredentials()`:** These are mutually exclusive in CORS. Use `WithOrigins(...)` when `AllowCredentials()` is set.
- **Omitting `ClockSkew = TimeSpan.Zero`:** By default, ASP.NET Core adds a 5-minute clock skew tolerance to JWT expiry. Without explicitly setting it to zero, a 15-minute token is valid for up to 20 minutes.
- **Querying refresh tokens by hash directly:** BCrypt hashes are not reversible and not queryable by value. The correct approach is to fetch candidates by time window (e.g., created in last 8 days, not expired, not revoked) and iterate with `BCrypt.Verify`.
- **Using `Username` field for login without migration:** The current `Users` table has `Username`, not `Email`. AUTH-01 requires login by email. A migration to add the `Email` column is required before the login endpoint can be implemented.
- **EF Core GroupBy with non-aggregate projection:** EF Core only translates `GroupBy` to SQL `GROUP BY` when the projection contains only the key and aggregate functions. In-memory computation after a single batch query is safer and more readable for this dataset size.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JWT validation | Custom token parsing middleware | `Microsoft.AspNetCore.Authentication.JwtBearer` | Signature validation, expiry, clock skew, issuer/audience — all handled correctly; custom code misses edge cases |
| Password hashing | SHA-256 / MD5 / custom | `BCrypt.Net-Next` v4 | BCrypt has adaptive cost factor; SHA-256 is fast and not suitable for passwords |
| Role authorization on route groups | Per-endpoint `RequireAuthorization` calls | `MapGroup(...).RequireAuthorization("AdminOnly")` | One call gates the entire group; easy to miss individual endpoints |
| Refresh token lookup by value | Indexing BCrypt hashes | Time-window candidate fetch + BCrypt.Verify loop | BCrypt output is not deterministic with the same input; cannot index or query by hash value |
| Aggregate team stats SQL | Raw SQL string queries | EF Core Include + in-memory LINQ | Dataset is tiny; EF Core handles it; raw SQL loses type safety and maintainability |

**Key insight:** The refresh token lookup pattern (fetch candidates, BCrypt.Verify in a loop) is counterintuitive but correct. The alternative — using a faster hash like HMAC-SHA256 and querying by hash — is also valid and more performant at scale, but the project has locked in BCrypt for consistency. At this scale (a few tokens per user, 5 users max), the performance difference is irrelevant.

---

## Common Pitfalls

### Pitfall 1: User Schema Has `Username`, Not `Email`

**What goes wrong:** The existing `Users` table (migration `20260218024436_InitialCreate`) has a `Username` column and no `Email` column. AUTH-01 requires login by email. Writing login logic against the current schema will silently use the wrong field.

**Why it happens:** The entity was scaffolded in Phase 1 before the auth requirements were fully specified.

**How to avoid:** Plan 03-01 must start with a migration that:
1. Adds `Email` column (non-nullable after data migration) with a unique index
2. Renames or removes `Username` (or keeps it as an optional display name — decision for the planner)
3. Seeds at least one admin user so the API is usable from day one

**Warning signs:** If login endpoint compiles and tests pass but `Users.Email` property does not exist on the entity — the migration was skipped.

### Pitfall 2: JWT Role Claim vs IsAdmin Bool

**What goes wrong:** `user.IsAdmin` is a `bool`. ASP.NET Core's `RequireRole("Admin")` checks `ClaimTypes.Role` for the string `"Admin"`. If the JWT is generated with a custom claim name like `"isAdmin": true`, the policy check fails with 403 on every admin endpoint.

**Why it happens:** Copying JWT examples that use custom claim names without understanding that `ClaimTypes.Role` is special-cased by ASP.NET Core's role middleware.

**How to avoid:** Always use `new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")` in the claims array. Verify with `dotnet user-jwts` tooling or a JWT decoder that the decoded token contains `"role": "Admin"`.

**Warning signs:** Admin endpoints return 403 even with a valid JWT for an admin user.

### Pitfall 3: CORS AllowAnyOrigin + AllowCredentials Conflict

**What goes wrong:** Setting `AllowAnyOrigin()` and `AllowCredentials()` together throws a runtime exception: "The CORS protocol does not allow specifying a wildcard origin and credentials at the same time."

**Why it happens:** CORS spec prohibits `Access-Control-Allow-Origin: *` with credentials. The React frontend needs credentials (for cookie-based refresh tokens or Authorization headers).

**How to avoid:** Use `WithOrigins("http://localhost:3000", "http://localhost:5173")` — never `AllowAnyOrigin()` when `AllowCredentials()` is set.

### Pitfall 4: Refresh Token BCrypt Verification Performance

**What goes wrong:** The refresh endpoint fetches ALL non-expired tokens from the DB and runs BCrypt.Verify on every one. With many users and many tokens, this becomes slow (BCrypt is intentionally slow).

**Why it happens:** BCrypt hashes cannot be queried by value — you must verify them in memory.

**How to avoid:** Scope the candidate query tightly: filter to `CreatedAt > DateTime.UtcNow.AddDays(-8)` (7-day expiry + 1 day buffer). This limits candidates to the recent window. At this project's scale (5 users, one active token each), this is a non-issue. Flag as a scalability concern for future reference.

**Warning signs:** Refresh endpoint takes >1 second at this scale — means candidate scoping is too broad.

### Pitfall 5: Missing `UseAuthentication()` / `UseAuthorization()` Middleware Order

**What goes wrong:** All endpoints return 401 even with valid tokens, OR authenticated endpoints are accessible without tokens — depending on which middleware is missing or in the wrong order.

**Why it happens:** In .NET 9, `WebApplication` auto-registers auth middleware when services are configured — but only if `UseCors()` is not called (which changes the required explicit ordering).

**How to avoid:** Always call explicitly in this order when CORS is involved:
1. `app.UseCors("FrontendDev")`
2. `app.UseAuthentication()`
3. `app.UseAuthorization()`

**Source:** https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/security?view=aspnetcore-9.0 — "In some cases, such as controlling middleware order, it's necessary to explicitly register authentication and authorization."

### Pitfall 6: EF Core AtsResult Enum Comparison in LINQ

**What goes wrong:** Comparing `g.GameResult.HomeAtsResult == AtsResult.Cover` in a LINQ query sent to the DB fails because EF Core stores the enum as a string (`"Cover"`) but the default comparison generates an integer comparison.

**Why it happens:** The DbContext's `OnModelCreating` configures `HasConversion<string>()` for ATS/OU enums. This works for storage, but LINQ queries that compare enum values must go through EF Core's query translation.

**How to avoid:** When filtering in-database, use string comparison explicitly or ensure EF Core's conversion is applied. The safest approach for aggregate stats: load the data into memory with `.ToListAsync()` first, then apply enum comparisons in C# — which is what Pattern 7 recommends. Alternatively, compare against the string value: `.Where(g => g.GameResult.HomeAtsResult == "Cover")` — but this requires changing the entity property type to string, which breaks the enum design. The in-memory approach is cleaner.

**Warning signs:** `InvalidOperationException: The LINQ expression could not be translated` when filtering by AtsResult in a DB-level Where clause.

### Pitfall 7: `ClockSkew` Default Causes Unexpected Token Validity

**What goes wrong:** Tokens with a 15-minute expiry are still accepted 18-20 minutes after issue. Unit tests checking token expiry at exactly 15 minutes pass in CI but users notice their sessions persist longer than expected.

**Why it happens:** `TokenValidationParameters.ClockSkew` defaults to 5 minutes in ASP.NET Core.

**How to avoid:** Set `ClockSkew = TimeSpan.Zero` explicitly in `AddJwtBearer` configuration.

---

## Code Examples

Verified patterns from official sources:

### Full Program.cs Setup

```csharp
// Source: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/security?view=aspnetcore-9.0
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NbaTracker.Api.Endpoints;
using NbaTracker.Api.Services;
using NbaTracker.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// DbContext (existing from Phase 1)
builder.Services.AddDbContext<NbaTrackerDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        x => x.MigrationsAssembly("NbaTracker.Data")));

// Token service
builder.Services.AddScoped<TokenService>();

// JWT auth
var jwtSecret = builder.Configuration["Jwt__Secret"]
    ?? throw new InvalidOperationException("Jwt__Secret must be configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "nbatracker-api",
            ValidateAudience = true,
            ValidAudience = "nbatracker-client",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddOpenApi();

var app = builder.Build();

// Migration in development (existing pattern from Phase 1)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NbaTrackerDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Migration error at startup");
    }
}

// Explicit middleware ordering required when CORS is involved
app.UseCors("FrontendDev");
app.UseAuthentication();
app.UseAuthorization();

// Route groups
var api = app.MapGroup("/api");

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

AuthEndpoints.Map(api.MapGroup("/auth"));
AdminEndpoints.Map(api.MapGroup("/admin").RequireAuthorization("AdminOnly"));
TeamEndpoints.Map(api.MapGroup("/teams").RequireAuthorization());

app.Run();
```

### Email Migration

```csharp
// New migration: AddEmailToUsers
// dotnet ef migrations add AddEmailToUsers --project src/NbaTracker.Data --startup-project src/NbaTracker.Api

migrationBuilder.AddColumn<string>(
    name: "Email",
    table: "Users",
    type: "text",
    nullable: false,
    defaultValue: "");

migrationBuilder.CreateIndex(
    name: "IX_Users_Email",
    table: "Users",
    column: "Email",
    unique: true);
```

And update the User entity:
```csharp
// src/NbaTracker.Data/Entities/User.cs
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;       // login credential — unique
    public string Username { get; set; } = null!;    // optional display name (keep for now)
    public string PasswordHash { get; set; } = null!;
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
```

### Admin Seeding

For the API to be usable after Phase 3 completes, at least one admin user must exist. Options:
1. Migration data seeder (simple but hardcodes a password in source control — bad)
2. An environment-variable-driven seed in `Program.cs` on first startup — preferable

```csharp
// In Program.cs after migration, before app.Run()
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NbaTrackerDbContext>();
    var adminEmail = app.Configuration["Seed__AdminEmail"];
    var adminPassword = app.Configuration["Seed__AdminPassword"];

    if (adminEmail is not null && adminPassword is not null
        && !await db.Users.AnyAsync(u => u.Email == adminEmail))
    {
        db.Users.Add(new User
        {
            Email = adminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            IsAdmin = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        app.Logger.LogInformation("Seeded admin user: {Email}", adminEmail);
    }
}
```

Add `Seed__AdminEmail` and `Seed__AdminPassword` to `.env` and `.env.example`.

### Home/Away Splits Query

```csharp
// GET /api/teams/{id}/stats — aggregate stats with home/away splits
// Load all FINAL games for this team, compute splits in memory
var homeGames = await db.Games
    .Where(g => g.HomeTeamId == id && g.Status == "FINAL")
    .Include(g => g.GameResult)
    .Include(g => g.GameLine)
    .ToListAsync(ct);

var awayGames = await db.Games
    .Where(g => g.AwayTeamId == id && g.Status == "FINAL")
    .Include(g => g.GameResult)
    .Include(g => g.GameLine)
    .ToListAsync(ct);

// Home ATS stats
int homeAtsCovers = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Cover);
int homeAtsLosses = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Loss);
int homeAtsPushes = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Push);

// Away ATS stats
int awayAtsCovers = awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Cover);
// ...and so on
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `AddAuthentication().AddJwtBearer(o => { ... })` in `Configure()` / `ConfigureServices()` | `builder.Services.AddAuthentication().AddJwtBearer()` in `Program.cs` with top-level statements | .NET 6 (2021) | No `Startup.cs`; everything is in `Program.cs` |
| `app.UseRouting()` + `app.UseEndpoints()` required | Not needed in Minimal APIs — routing is built in | .NET 6 (2021) | Removes boilerplate; `MapGet` etc. directly on `app` |
| `[Authorize(Roles = "Admin")]` attribute on controllers | `.RequireAuthorization("AdminOnly")` on route group | .NET 7+ Minimal API | Policy-based authorization via `MapGroup` is the Minimal API pattern |
| `services.AddAuthorization(options => { ... })` | `builder.Services.AddAuthorizationBuilder().AddPolicy(...)` | .NET 7+ | `AddAuthorizationBuilder` is the fluent API; both work but the builder is newer |
| Manual `UseAuthentication()` / `UseAuthorization()` calls always required | Auto-registered by `WebApplication` when services are configured | .NET 7+ | BUT explicit calls are still needed when CORS middleware is used |

**Deprecated/outdated:**
- `Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler`: A newer alternative to `JwtSecurityTokenHandler`, but `JwtSecurityTokenHandler` remains the standard and is fully supported in .NET 9. Do not mix both in the same codebase.
- `Startup.cs` / `ConfigureServices()` / `Configure()` pattern: Fully replaced by the top-level statement `Program.cs` pattern used throughout this project.

---

## Open Questions

1. **`Username` column disposition after adding `Email`**
   - What we know: The `Users` table currently has `Username` (nullable strategy unknown); AUTH-01 requires login by email. The prior decision only specifies `Email` and `PasswordHash` for the auth schema.
   - What's unclear: Should `Username` be kept as a display name (useful in Phase 4 for the UI), renamed to `Email`, or dropped?
   - Recommendation: Keep `Username` as an optional display name column. Add `Email` as a new required column with unique index. This preserves the option to show a display name in the UI without another migration later.

2. **Refresh token storage: response body vs HttpOnly cookie**
   - What we know: Prior decisions specify JWT access token + refresh token, with `TokenHash` in DB. The storage mechanism (HttpOnly cookie vs response body) is not locked.
   - What's unclear: React frontend (Phase 4) needs to know how to store/send the refresh token. HttpOnly cookie is more secure (not accessible to JS) but requires `SameSite` configuration and cookie handling in CORS. Response body + localStorage is simpler to implement.
   - Recommendation: Return refresh token in the response body for Phase 3 (simpler to test with curl/Swagger). Document that Phase 5 should revisit with HttpOnly cookie for production security.

3. **`/api/admin/sync-status` endpoint ownership**
   - What we know: The 02-ingestion-worker deferred-items.md lists this endpoint as "Phase 3 only — Phase 2 writes the right data to sync_runs."
   - What's unclear: Should this be plan 03-01 (alongside admin endpoints) or plan 03-02 (alongside data query endpoints)?
   - Recommendation: Include in plan 03-01 with admin user creation — it's an admin endpoint, reads from `SyncRuns` which already exists, and is low complexity.

---

## Sources

### Primary (HIGH confidence)

- [Microsoft Learn — Authentication and authorization in Minimal APIs (ASP.NET Core 9)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/security?view=aspnetcore-9.0) — JWT Bearer setup, RequireAuthorization, MapGroup with auth, middleware ordering with CORS
- [Microsoft Learn — Route handlers in Minimal APIs (ASP.NET Core 9)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/route-handlers?view=aspnetcore-9.0) — MapGroup pattern, static endpoint classes, route parameters
- [Microsoft Learn — Role-based authorization in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-9.0) — RequireRole, AddAuthorizationBuilder, ClaimTypes.Role
- [Microsoft Learn — Complex Query Operators in EF Core](https://learn.microsoft.com/en-us/ef/core/querying/complex-query-operators) — GroupBy translation, LEFT JOIN, aggregate operators supported in SQL translation
- [NuGet: BCrypt.Net-Next 4.x](https://www.nuget.org/packages/BCrypt.Net-Next) — HashPassword, Verify, work factor
- Existing codebase (read directly): `NbaTracker.Api/Program.cs`, `NbaTracker.Api/NbaTracker.Api.csproj`, `NbaTracker.Data/Entities/User.cs`, `NbaTracker.Data/Entities/RefreshToken.cs`, `NbaTracker.Data/NbaTrackerDbContext.cs`, `NbaTracker.Data/Migrations/20260218024436_InitialCreate.cs`

### Secondary (MEDIUM confidence)

- WebSearch: CORS AllowAnyOrigin + AllowCredentials incompatibility — confirmed against official ASP.NET Core CORS docs pattern (multiple sources agree)
- WebSearch: BCrypt.Net-Next HashPassword / Verify usage — consistent across multiple tutorials; matches official GitHub README
- WebSearch: JWT refresh token rotation patterns (revoke-old/issue-new) — standard pattern documented across multiple ASP.NET Core tutorials

### Tertiary (LOW confidence)

- Refresh token lookup strategy (time-window candidate fetch + BCrypt.Verify loop): Inferred from BCrypt's non-reversible nature. No official source documents this exact pattern, but it follows logically from BCrypt semantics. Alternative (HMAC-SHA256) is a valid and common approach but requires changing the storage design from the locked decision.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — packages verified from official Microsoft docs and NuGet; JwtBearer 9.x, BCrypt.Net-Next 4.x confirmed current
- Architecture patterns: HIGH — MapGroup, RequireAuthorization, JWT setup from official ASP.NET Core 9 docs
- EF Core query patterns: HIGH — official EF Core docs; in-memory aggregate approach verified against scale constraints
- Schema issue (Email migration): HIGH — confirmed by reading the actual migration file; `Username` column exists, `Email` column does not
- Refresh token BCrypt lookup: MEDIUM — logically correct but no official reference documents this specific pattern
- Pitfalls: HIGH for CORS/auth/clock-skew (documented in official sources); MEDIUM for BCrypt lookup performance

**Research date:** 2026-02-19
**Valid until:** 2026-04-19 (stable technologies — 60 days; BCrypt.Net-Next and JwtBearer are long-stable packages)
