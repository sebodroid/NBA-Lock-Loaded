# Phase 1: Foundation - Research

**Researched:** 2026-02-17
**Domain:** Docker Compose multi-container orchestration, .NET 9 multi-project solution structure, EF Core 9 migrations with shared class library, secrets management
**Confidence:** HIGH

---

## Summary

Phase 1 establishes the entire infrastructure layer before a single line of application logic is written. It has two distinct work streams: (1) scaffolding four Docker containers that can start reliably in the correct order, and (2) creating the shared .NET class library (`NbaTracker.Data`) with a correctly modeled EF Core schema and working migrations.

The primary challenge is not any individual technology — Docker Compose, EF Core, and .NET multi-project solutions are all well-documented — but their interaction. The Docker build context must be the solution root (not a per-project directory) so each Dockerfile can `COPY` the shared `NbaTracker.Data` project files during `dotnet restore`. EF Core migrations require an `IDesignTimeDbContextFactory` in the shared Data project so `dotnet ef` commands can discover the DbContext without a startup project providing DI. Migrations should be applied programmatically at startup in development (via `MigrateAsync()`) and via scripts or migration bundles in production.

Secrets management is straightforward: a `.env` file at the solution root, referenced via `env_file:` in `docker-compose.yml`, and immediately added to `.gitignore`. The Worker container uses `mcr.microsoft.com/dotnet/runtime:9.0` (not aspnet) since it has no HTTP server dependencies.

**Primary recommendation:** Set Docker build context to the solution root for both API and Worker Dockerfiles. This is the only way to `COPY` the shared `NbaTracker.Data` project during the Docker build, and getting it wrong requires a full restructure later.

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK | 9.0 | Build target for all projects | Current STS release; perf improvements for Minimal API routing |
| ASP.NET Core Minimal API | 9.x | API project framework | Standard for new .NET web APIs; less boilerplate than MVC |
| .NET Worker Service | 9.x | Worker project framework | Standard pattern for background services in .NET |
| EF Core | 9.x | ORM for schema definition and querying | Standard .NET ORM; handles migrations natively |
| Npgsql.EntityFrameworkCore.PostgreSQL | 9.x | PostgreSQL driver for EF Core | Only maintained PostgreSQL provider for EF Core |
| Microsoft.EntityFrameworkCore.Design | 9.x | EF Core design-time tooling | Required for `dotnet ef migrations add` |
| postgres | 16-alpine | Local dev database container | Current stable PostgreSQL; alpine for smaller image |
| Docker Compose | v2 (Compose Spec) | Multi-container orchestration | Standard local dev orchestration; no version field needed |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| mcr.microsoft.com/dotnet/sdk:9.0 | 9.0 | Dockerfile build stage | Build stage only — never as runtime base |
| mcr.microsoft.com/dotnet/aspnet:9.0 | 9.0 | API container runtime base | API project (has ASP.NET runtime) |
| mcr.microsoft.com/dotnet/runtime:9.0 | 9.0 | Worker container runtime base | Worker project — smaller than aspnet, no HTTP server needed |
| node:20-alpine | 20-alpine | Frontend build stage | React/Vite build |
| nginx:alpine | latest | Frontend runtime container | Serves built React static files |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `mcr.microsoft.com/dotnet/runtime:9.0` for Worker | `aspnet:9.0` | aspnet image is ~60MB larger; no benefit for a non-HTTP Worker Service |
| `.env` file for secrets | Docker Secrets (`/run/secrets/`) | Docker Secrets are more secure in production but add complexity; .env + gitignore is standard for local dev |
| `MigrateAsync()` at startup | Separate migration container | Migration container is cleaner for production CI/CD; `MigrateAsync()` is acceptable for local dev |
| Compose Spec (no version field) | `version: "3.9"` | The `version` field is deprecated in Docker Compose v2; omit it |

**Installation:**
```bash
# .NET projects — add to NbaTracker.Data.csproj
dotnet add package Microsoft.EntityFrameworkCore --version 9.*
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 9.*
dotnet add package Microsoft.EntityFrameworkCore.Design --version 9.*

# .NET projects — add to API and Worker .csproj
# (project reference to NbaTracker.Data, not NuGet packages)
dotnet add reference ../NbaTracker.Data/NbaTracker.Data.csproj
```

---

## Architecture Patterns

### Recommended Project Structure

```
nba-lines-tracker/              ← solution root, also Docker build context
├── .env                        ← secrets (gitignored)
├── .env.example                ← committed template with placeholder values
├── .gitignore                  ← must include .env
├── docker-compose.yml          ← all four containers
├── NbaTracker.sln
├── src/
│   ├── NbaTracker.Api/         ← ASP.NET Core Minimal API
│   │   ├── NbaTracker.Api.csproj
│   │   ├── Program.cs
│   │   └── Dockerfile          ← build context: solution root (../../)
│   ├── NbaTracker.Worker/      ← .NET Worker Service
│   │   ├── NbaTracker.Worker.csproj
│   │   ├── Program.cs
│   │   └── Dockerfile          ← build context: solution root (../../)
│   └── NbaTracker.Data/        ← shared class library
│       ├── NbaTracker.Data.csproj
│       ├── NbaTrackerDbContext.cs
│       ├── Entities/
│       │   ├── Team.cs
│       │   ├── Game.cs
│       │   ├── GameLine.cs
│       │   ├── GameResult.cs
│       │   ├── User.cs
│       │   ├── RefreshToken.cs
│       │   └── SyncRun.cs
│       ├── DesignTimeDbContextFactory.cs   ← required for dotnet ef commands
│       └── Migrations/
└── frontend/
    ├── package.json
    ├── vite.config.ts
    └── Dockerfile
```

### Pattern 1: Solution-Root Docker Build Context

**What:** Every service in `docker-compose.yml` specifies `context: .` (the solution root), with individual Dockerfiles pointing to the project subdirectory via the `dockerfile:` key. This gives each Dockerfile access to the entire solution, enabling `COPY` of the shared `NbaTracker.Data` project.

**When to use:** Any .NET multi-project solution where a Dockerfile needs files from outside its own project directory.

**Example:**
```yaml
# docker-compose.yml (solution root)
services:
  api:
    build:
      context: .
      dockerfile: src/NbaTracker.Api/Dockerfile
  worker:
    build:
      context: .
      dockerfile: src/NbaTracker.Worker/Dockerfile
```

```dockerfile
# src/NbaTracker.Api/Dockerfile
# Source: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Copy solution and all csproj files first (layer cache for dotnet restore)
COPY NbaTracker.sln .
COPY src/NbaTracker.Api/NbaTracker.Api.csproj ./src/NbaTracker.Api/
COPY src/NbaTracker.Data/NbaTracker.Data.csproj ./src/NbaTracker.Data/
COPY src/NbaTracker.Worker/NbaTracker.Worker.csproj ./src/NbaTracker.Worker/
RUN dotnet restore src/NbaTracker.Api/NbaTracker.Api.csproj

# Copy all source and publish
COPY src/ ./src/
WORKDIR /source/src/NbaTracker.Api
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "NbaTracker.Api.dll"]
```

```dockerfile
# src/NbaTracker.Worker/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

COPY NbaTracker.sln .
COPY src/NbaTracker.Api/NbaTracker.Api.csproj ./src/NbaTracker.Api/
COPY src/NbaTracker.Data/NbaTracker.Data.csproj ./src/NbaTracker.Data/
COPY src/NbaTracker.Worker/NbaTracker.Worker.csproj ./src/NbaTracker.Worker/
RUN dotnet restore src/NbaTracker.Worker/NbaTracker.Worker.csproj

COPY src/ ./src/
WORKDIR /source/src/NbaTracker.Worker
RUN dotnet publish -c Release -o /app --no-restore

# Worker uses runtime, not aspnet — no HTTP server needed
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "NbaTracker.Worker.dll"]
```

### Pattern 2: PostgreSQL Health Check + depends_on

**What:** The PostgreSQL container exposes a `healthcheck` using `pg_isready`. The API and Worker containers use `depends_on` with `condition: service_healthy` to guarantee PostgreSQL is accepting connections before they start.

**When to use:** Any time an application container must connect to PostgreSQL on startup (including EF Core `MigrateAsync()` calls).

**Example:**
```yaml
# docker-compose.yml
# Source: https://docs.docker.com/compose/how-tos/startup-order/
services:
  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: nbatracker
      POSTGRES_USER: ${DB_USER}
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -d nbatracker -U ${DB_USER}"]
      interval: 5s
      timeout: 5s
      retries: 5
      start_period: 10s

  api:
    build:
      context: .
      dockerfile: src/NbaTracker.Api/Dockerfile
    ports:
      - "5000:8080"
    env_file:
      - .env
    depends_on:
      db:
        condition: service_healthy

  worker:
    build:
      context: .
      dockerfile: src/NbaTracker.Worker/Dockerfile
    env_file:
      - .env
    depends_on:
      db:
        condition: service_healthy

  frontend:
    build:
      context: frontend
      dockerfile: Dockerfile
    ports:
      - "3000:80"

volumes:
  pgdata:
```

### Pattern 3: IDesignTimeDbContextFactory in Shared Data Project

**What:** When `NbaTracker.Data` is a class library (no `Program.cs`), the `dotnet ef` tool cannot discover the DbContext through the normal DI pipeline. An `IDesignTimeDbContextFactory<NbaTrackerDbContext>` implementation in the same project tells the tool exactly how to construct the DbContext at design time.

**When to use:** Required whenever `DbContext` lives in a class library project rather than a startup project.

**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation
// src/NbaTracker.Data/DesignTimeDbContextFactory.cs
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NbaTrackerDbContext>
{
    public NbaTrackerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NbaTrackerDbContext>();

        // Design-time connection string — connects to local dev PostgreSQL
        // Never hardcode production credentials here
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=nbatracker;Username=dev;Password=devpassword";

        optionsBuilder.UseNpgsql(connectionString,
            x => x.MigrationsAssembly("NbaTracker.Data"));

        return new NbaTrackerDbContext(optionsBuilder.Options);
    }
}
```

Running migrations from the solution root:
```bash
# --project = where DbContext lives (and where migrations will be created)
# --startup-project = any executable project that references NbaTracker.Data
dotnet ef migrations add InitialCreate \
  --project src/NbaTracker.Data \
  --startup-project src/NbaTracker.Api

dotnet ef database update \
  --project src/NbaTracker.Data \
  --startup-project src/NbaTracker.Api
```

### Pattern 4: DbContext Registration with MigrationsAssembly

**What:** When the `DbContext` and its migrations both live in `NbaTracker.Data`, but both API and Worker register the DbContext, both must specify `MigrationsAssembly` to point EF Core at the correct assembly.

**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/projects
// In NbaTracker.Api/Program.cs AND NbaTracker.Worker/Program.cs
builder.Services.AddDbContext<NbaTrackerDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        x => x.MigrationsAssembly("NbaTracker.Data")));
```

### Pattern 5: Apply Migrations at Startup (Development Only)

**What:** For local development, `MigrateAsync()` ensures the local PostgreSQL container's schema is always up to date. This is acceptable for development but not recommended for production.

**Example:**
```csharp
// In NbaTracker.Api/Program.cs
// Apply migrations automatically in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NbaTrackerDbContext>();
    await db.Database.MigrateAsync();
}
```

### Anti-Patterns to Avoid

- **Build context = project directory:** If `context:` points to `src/NbaTracker.Api/`, the Dockerfile cannot `COPY` files from `src/NbaTracker.Data/`. The build will fail on `dotnet restore` with "Unable to find project" errors.
- **`dotnet/aspnet` base image for Worker:** The aspnet image includes Kestrel, middleware, and other ASP.NET Core runtime components a Worker Service never uses — ~60MB wasted.
- **Secrets inline in docker-compose.yml:** Any `password: mysecret` inline in Compose YAML gets committed to git. All secrets must come from environment variables via `env_file:` or environment variable expansion.
- **No version field in Compose:** Docker Compose v2 uses the Compose Specification. The `version:` field is deprecated and ignored. Omit it.
- **`depends_on` without health checks:** Without `condition: service_healthy`, `depends_on: db` only waits for PostgreSQL to start the process — not to accept connections. The API container will crash on startup because PostgreSQL isn't ready.
- **Migrations outside NbaTracker.Data:** If migrations are generated in the API or Worker project, they won't be accessible to the other service that also uses the DbContext.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Container startup ordering | Custom wait scripts (`wait-for-it.sh`) | Docker Compose `healthcheck` + `depends_on: condition: service_healthy` | Native, no shell scripts needed; `pg_isready` is built into the postgres image |
| Schema versioning | SQL scripts in source control | EF Core migrations | Migrations track schema history, generate SQL for any provider, handle rollbacks |
| DbContext construction for `dotnet ef` | Custom build scripts | `IDesignTimeDbContextFactory<T>` | Official EF Core pattern; tools call it automatically |
| Password hashing | BCrypt implementation | `BCrypt.Net-Next` NuGet package | Argon2/BCrypt have complexity traps; use a maintained library |
| Connection string templating | Custom env var parser | ASP.NET Core Configuration + `env_file:` in Compose | Configuration system handles `ConnectionStrings__Default` → `ConnectionStrings:Default` mapping via double-underscore convention |

**Key insight:** Docker Compose health checks + `service_healthy` condition eliminate the entire category of "container starts before dependency is ready" problems that traditionally required shell script workarounds.

---

## Common Pitfalls

### Pitfall 1: Docker Build Fails Because Shared Project Not Found

**What goes wrong:** `docker build` runs with the context set to `src/NbaTracker.Api/`, tries to execute `COPY src/NbaTracker.Data/NbaTracker.Data.csproj`, and fails because `src/NbaTracker.Data/` is not inside the build context directory. The error is `COPY failed: file not found in build context`.

**Why it happens:** Docker build context defines the filesystem root the Dockerfile can access. Only files inside the context directory can be `COPY`'d.

**How to avoid:** Set `context: .` (solution root) in `docker-compose.yml` for every service. Keep Dockerfiles inside their project subdirectories and reference them via `dockerfile: src/NbaTracker.Api/Dockerfile`.

**Warning signs:** Any error mentioning "file not found in build context" during `docker compose build`.

### Pitfall 2: `dotnet ef` Cannot Find DbContext in Class Library

**What goes wrong:** Running `dotnet ef migrations add` against `NbaTracker.Data` fails with "Unable to create an object of type 'NbaTrackerDbContext'" because the class library has no `Program.cs` and no DI container that EF Core tools can bootstrap.

**Why it happens:** EF Core tools look for the DbContext either through the application's DI container (via `Program.CreateHostBuilder()`) or through an `IDesignTimeDbContextFactory`. A class library has neither by default.

**How to avoid:** Add `DesignTimeDbContextFactory.cs` to `NbaTracker.Data` implementing `IDesignTimeDbContextFactory<NbaTrackerDbContext>`. Always specify both `--project` and `--startup-project` when running `dotnet ef` commands.

**Warning signs:** Error "Unable to create an object of type" or "No DbContext was found" when running EF Core CLI commands.

### Pitfall 3: PostgreSQL Container Not Ready When .NET Container Starts

**What goes wrong:** On `docker compose up`, the API container starts before PostgreSQL finishes initializing and calls `MigrateAsync()` — which fails with "Connection refused." Docker restarts the container, PostgreSQL is ready by then, but the restart is random and inconsistent.

**Why it happens:** `depends_on: db` without a health condition only waits for the container process to start, not for the service to be ready.

**How to avoid:** Add a `healthcheck` to the `db` service using `pg_isready` and use `depends_on: db: condition: service_healthy` on both API and Worker services.

**Warning signs:** `docker compose up` sometimes succeeds on first try, sometimes fails — timing-dependent behavior.

### Pitfall 4: Secrets Committed to Git

**What goes wrong:** A JWT secret, database password, or API key appears in `docker-compose.yml`, `appsettings.json`, or `appsettings.Development.json` and gets committed. The Aiven connection string contains credentials in plain text.

**Why it happens:** Developers put real values in config files for convenience during initial setup and forget to remove them before the first commit.

**How to avoid:** Before writing any secret value anywhere, add `.env` to `.gitignore`. Provide `.env.example` with clearly fake placeholder values (e.g., `DB_PASSWORD=REPLACE_ME`). Use `env_file: .env` in docker-compose.yml, never inline environment values.

**Warning signs:** Any `git diff` that shows a real password, token, or connection string string with credentials.

### Pitfall 5: Aiven PostgreSQL SSL Connection Rejection

**What goes wrong:** Local dev works with plain PostgreSQL. When connecting to Aiven, connections fail with SSL-related errors because Aiven requires SSL and Npgsql defaults to `SSL Mode=Prefer` without certificate validation.

**Why it happens:** Aiven PostgreSQL requires encrypted connections. The default Npgsql SSL mode may not satisfy Aiven's requirements, and the Aiven CA certificate may not be in the system trust store.

**How to avoid:** Use `SSL Mode=Require;Trust Server Certificate=true` for development. For production, use `SSL Mode=VerifyFull` with Aiven's CA certificate. Test the Aiven connection string explicitly before declaring Phase 1 complete.

**Warning signs:** Connection works locally but fails when pointed at Aiven. Any SSL handshake error in connection logs.

### Pitfall 6: Worker Service Uses Wrong Runtime Image

**What goes wrong:** Worker Dockerfile uses `mcr.microsoft.com/dotnet/aspnet:9.0` instead of `mcr.microsoft.com/dotnet/runtime:9.0`. The image builds and runs correctly but is ~60MB larger with no benefit. This is a low-severity pitfall for local dev but becomes a deployment concern.

**Why it happens:** Developers copy the API Dockerfile and forget to change the base image.

**How to avoid:** Worker Services with no HTTP server should always use `mcr.microsoft.com/dotnet/runtime:9.0` as the final stage base image.

---

## Code Examples

Verified patterns from official sources:

### docker-compose.yml Complete Pattern

```yaml
# Source: https://docs.docker.com/compose/how-tos/startup-order/
# Note: No "version:" field — deprecated in Docker Compose v2

services:
  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: nbatracker
      POSTGRES_USER: ${DB_USER}
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -d nbatracker -U ${DB_USER}"]
      interval: 5s
      timeout: 5s
      retries: 5
      start_period: 10s

  api:
    build:
      context: .
      dockerfile: src/NbaTracker.Api/Dockerfile
    ports:
      - "5000:8080"
    env_file:
      - .env
    depends_on:
      db:
        condition: service_healthy

  worker:
    build:
      context: .
      dockerfile: src/NbaTracker.Worker/Dockerfile
    env_file:
      - .env
    depends_on:
      db:
        condition: service_healthy

  frontend:
    build:
      context: frontend
      dockerfile: Dockerfile
    ports:
      - "3000:80"

volumes:
  pgdata:
```

### .env File and .env.example

```bash
# .env (gitignored — real values)
DB_USER=dev
DB_PASSWORD=devpassword_local
ConnectionStrings__Default=Host=db;Port=5432;Database=nbatracker;Username=dev;Password=devpassword_local
JWT__Secret=generate_with_openssl_rand_base64_32
BDLAPI__Key=your_balldontlie_key
OddsAPI__Key=your_theoddsapi_key
```

```bash
# .env.example (committed — placeholder values)
DB_USER=REPLACE_ME
DB_PASSWORD=REPLACE_ME
ConnectionStrings__Default=Host=db;Port=5432;Database=nbatracker;Username=REPLACE_ME;Password=REPLACE_ME
JWT__Secret=REPLACE_WITH_32_BYTE_BASE64_SECRET
BDLAPI__Key=REPLACE_ME
OddsAPI__Key=REPLACE_ME
```

### EF Core Entity Scaffold (Key Tables for Phase 1)

```csharp
// Source: Based on ARCHITECTURE.md schema + PITFALLS.md guidance
// src/NbaTracker.Data/Entities/Team.cs
public class Team
{
    public int Id { get; set; }
    public string NbaApiId { get; set; } = null!;   // PITFALL-01: separate external ID column
    public string Name { get; set; } = null!;
    public string Abbreviation { get; set; } = null!;
    public string? Conference { get; set; }
    public string? Division { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Game> HomeGames { get; set; } = [];
    public ICollection<Game> AwayGames { get; set; } = [];
}

// src/NbaTracker.Data/Entities/Game.cs
public class Game
{
    public int Id { get; set; }
    public string NbaGameId { get; set; } = null!;    // PITFALL-01: BallDontLie game ID
    public string? OddsApiGameId { get; set; }        // PITFALL-01: The Odds API game ID
    public DateOnly GameDate { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public int? HomeScore { get; set; }               // null until final
    public int? AwayScore { get; set; }
    public string Status { get; set; } = null!;       // SCHEDULED/FINAL/POSTPONED/etc — PITFALL-13
    public string Season { get; set; } = null!;       // "2024-25"
    public bool? WentToOvertime { get; set; }         // PITFALL-08
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;
    public GameLine? GameLine { get; set; }
    public GameResult? GameResult { get; set; }
}

// src/NbaTracker.Data/Entities/GameLine.cs
public class GameLine
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public decimal? Spread { get; set; }              // from favorite's perspective (always positive)
    public int? FavoriteTeamId { get; set; }          // PITFALL-06: explicit, not inferred from sign
    public decimal? Total { get; set; }
    public int? HomeSpreadOdds { get; set; }
    public int? AwaySpreadOdds { get; set; }
    public int? OverOdds { get; set; }
    public int? UnderOdds { get; set; }
    public string? Bookmaker { get; set; }            // FanDuel = canonical; HardRock = fallback
    public DateTime? LineTimestamp { get; set; }      // TIMESTAMPTZ — PITFALL-11
    public DateTime UpdatedAt { get; set; }

    public Game Game { get; set; } = null!;
}

// src/NbaTracker.Data/Entities/GameResult.cs
// ATS/O/U as 3-value enums — PITFALL-07
public enum AtsResult { Cover, Loss, Push }
public enum OuResult { Over, Under, Push }

public class GameResult
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public AtsResult? HomeAtsResult { get; set; }    // null if no line
    public AtsResult? AwayAtsResult { get; set; }
    public OuResult? OuResult { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public Game Game { get; set; } = null!;
}

// src/NbaTracker.Data/Entities/User.cs
public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!; // BCrypt hash
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

// src/NbaTracker.Data/Entities/RefreshToken.cs
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = null!;   // PITFALL-21: store hash, not plaintext
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}

// src/NbaTracker.Data/Entities/SyncRun.cs
public enum SyncRunStatus { Running, Success, Partial, Failed }

public class SyncRun
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public SyncRunStatus Status { get; set; }
    public int? GamesProcessed { get; set; }
    public string? ErrorDetails { get; set; }        // JSON blob of error info — PITFALL-18
    public string? Notes { get; set; }
}
```

### DbContext Registration

```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/projects
// src/NbaTracker.Data/NbaTrackerDbContext.cs
public class NbaTrackerDbContext : DbContext
{
    public NbaTrackerDbContext(DbContextOptions<NbaTrackerDbContext> options)
        : base(options) { }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameLine> GameLines => Set<GameLine>();
    public DbSet<GameResult> GameResults => Set<GameResult>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Store enums as strings for readability
        modelBuilder.Entity<GameResult>()
            .Property(r => r.HomeAtsResult)
            .HasConversion<string>();
        modelBuilder.Entity<GameResult>()
            .Property(r => r.AwayAtsResult)
            .HasConversion<string>();
        modelBuilder.Entity<GameResult>()
            .Property(r => r.OuResult)
            .HasConversion<string>();
        modelBuilder.Entity<SyncRun>()
            .Property(r => r.Status)
            .HasConversion<string>();

        // Indexes for common query patterns — PITFALL-14
        modelBuilder.Entity<Game>()
            .HasIndex(g => new { g.Season, g.Status });
        modelBuilder.Entity<Game>()
            .HasIndex(g => g.HomeTeamId);
        modelBuilder.Entity<Game>()
            .HasIndex(g => g.AwayTeamId);
        modelBuilder.Entity<Game>()
            .HasIndex(g => g.NbaGameId)
            .IsUnique();
        modelBuilder.Entity<Team>()
            .HasIndex(t => t.NbaApiId)
            .IsUnique();

        // Unique constraint on refresh token hash
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(r => r.TokenHash)
            .IsUnique();
    }
}
```

### Aiven PostgreSQL Connection String

```bash
# .env — for Aiven cloud connection
# Source: https://www.npgsql.org/doc/security.html
ConnectionStrings__Default=Host=<aiven-host>;Port=<port>;Database=<db>;Username=<user>;Password=<pass>;SSL Mode=Require;Trust Server Certificate=true

# For local dev PostgreSQL container (no SSL needed)
ConnectionStrings__Default=Host=db;Port=5432;Database=nbatracker;Username=dev;Password=devpassword
```

Note: `Host=db` uses the Docker service name (`db`) for inter-container networking. When connecting from the host machine (for `dotnet ef` commands), use `Host=localhost` and ensure port 5432 is exposed.

### Running EF Core Commands (From Solution Root)

```bash
# Add initial migration
dotnet ef migrations add InitialCreate \
  --project src/NbaTracker.Data \
  --startup-project src/NbaTracker.Api

# Apply to local dev database
dotnet ef database update \
  --project src/NbaTracker.Data \
  --startup-project src/NbaTracker.Api

# Apply to Aiven (override connection string)
dotnet ef database update \
  --project src/NbaTracker.Data \
  --startup-project src/NbaTracker.Api \
  --connection "Host=<aiven-host>;Port=<port>;Database=<db>;Username=<user>;Password=<pass>;SSL Mode=Require;Trust Server Certificate=true"
```

### Frontend Dockerfile (Vite + Nginx)

```dockerfile
# frontend/Dockerfile
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `version: "3.9"` in docker-compose.yml | No `version` field (Compose Spec) | Docker Compose v2 | Old `version` field is ignored; safe to omit |
| Separate `docker-compose` binary (v1) | `docker compose` subcommand (v2) | Docker Desktop 2022+ | Use `docker compose up` not `docker-compose up` |
| `wait-for-it.sh` shell scripts | `healthcheck` + `depends_on: condition: service_healthy` | Docker Compose 3.4+ | No custom scripts needed for startup ordering |
| EF Core migrations in startup project | Migrations in shared class library + `IDesignTimeDbContextFactory` | EF Core 5+ | Both API and Worker reference same DbContext and migrations |
| `mcr.microsoft.com/dotnet/core/aspnet` | `mcr.microsoft.com/dotnet/aspnet` | .NET 5 | Old `dotnet/core/` prefix retired; use `dotnet/aspnet` |

**Deprecated/outdated:**
- `docker-compose` (v1, Python-based): replaced by `docker compose` plugin built into Docker Desktop
- `version:` field in Compose YAML: deprecated, ignored by Compose v2
- `dotnet/core/aspnet` image prefix: replaced by `dotnet/aspnet`

---

## Open Questions

1. **Should migrations apply automatically on API startup in local dev?**
   - What we know: `MigrateAsync()` at startup is safe for local dev, risky for production (race conditions, distributed deploys)
   - What's unclear: Team preference for explicit vs. automatic migration application
   - Recommendation: Use `MigrateAsync()` in `IsDevelopment()` block for local dev simplicity; defer production migration strategy to Phase 5

2. **Which environment does `dotnet ef database update` target when run from host?**
   - What we know: When PostgreSQL container is running with port 5432 exposed, `dotnet ef database update` can reach it via `Host=localhost`. The `IDesignTimeDbContextFactory` reads `ConnectionStrings__Default` from environment or falls back to a hardcoded local default.
   - What's unclear: Whether developers will have `ConnectionStrings__Default` set in their host shell environment or need it set in the terminal before running EF commands
   - Recommendation: Hardcode a safe local-only fallback (`Host=localhost;...devpassword`) in `DesignTimeDbContextFactory.cs` so it works out of the box with the default Docker Compose setup

3. **Does the Worker project need to register the DbContext at all in Phase 1?**
   - What we know: The Worker has no sync logic yet in Phase 1 — it's a stub container. The DbContext registration isn't needed until Phase 2.
   - What's unclear: Whether to wire up full DI in the Worker stub now or keep it minimal
   - Recommendation: Register the DbContext in Worker's `Program.cs` in Phase 1 to confirm the project reference and connection compile and link correctly — this directly tests success criterion #3

---

## Sources

### Primary (HIGH confidence)

- [Microsoft Learn — Design-time DbContext Creation](https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation) — `IDesignTimeDbContextFactory` behavior, tool discovery order
- [Microsoft Learn — Using a Separate Migrations Project](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/projects) — `--project`/`--startup-project` flags, `MigrationsAssembly` configuration
- [Microsoft Learn — Run ASP.NET Core app in Docker containers (.NET 9)](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-9.0) — Official .NET 9 Dockerfile pattern with multi-stage build
- [Microsoft Learn — .NET container images](https://learn.microsoft.com/en-us/dotnet/core/docker/container-images) — `dotnet/runtime` vs `dotnet/aspnet` distinction
- [Npgsql Documentation — Security and Encryption](https://www.npgsql.org/doc/security.html) — SSL Mode options for cloud PostgreSQL
- [Docker Docs — Control startup order](https://docs.docker.com/compose/how-tos/startup-order/) — `healthcheck` + `depends_on: condition: service_healthy`

### Secondary (MEDIUM confidence)

- [End Point Dev — Using Docker Compose to Deploy a Multi-Application .NET System (2024)](https://www.endpointdev.com/blog/2024/07/using-docker-compose-to-deploy-a-multi-application-dotnet-system/) — Solution-root build context pattern, verified against official docs
- [Poespas Blog — ASP.NET Core EF Migrations Docker Compose (Feb 2025)](https://blog.poespas.me/posts/2025/02/14/aspnetcore-entityframework-migrations-docker-compose/) — Migration application patterns with Docker Compose

### Tertiary (LOW confidence — validate before use)

- WebSearch findings on Worker Service health checks: for a background worker with no HTTP endpoint, Docker `HEALTHCHECK` is harder to implement cleanly; for Phase 1 (stub containers), no health check on Worker is acceptable. Flag for Phase 2 when the Worker has actual running logic.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all packages from official Microsoft docs, versions confirmed for .NET 9
- Architecture: HIGH — build context and multi-project patterns confirmed against official docs and real-world 2024 examples
- EF Core migrations: HIGH — IDesignTimeDbContextFactory and --project flags confirmed from official Microsoft EF Core docs
- Docker health checks: HIGH — pg_isready and depends_on condition confirmed from official Docker docs
- Pitfalls: HIGH for infrastructure pitfalls (15-18 from PITFALLS.md are directly relevant); MEDIUM for schema pitfalls (06-09, 11 are relevant — confirmed from project research docs)
- Aiven SSL: MEDIUM — Npgsql docs confirm SSL Mode options; Aiven-specific behavior not independently verified

**Research date:** 2026-02-17
**Valid until:** 2026-04-17 (stable technologies — 60 days)
