# Architecture Research — NBA Lines Tracker

**Dimension**: Architecture
**Milestone**: Greenfield — How are sports data aggregation + analytics web apps typically structured?
**Date**: 2026-02-17
**Status**: Research complete

---

## Summary

A full-stack NBA betting analytics app with .NET/React/PostgreSQL/Docker is best structured as four discrete containers communicating through a shared database: a React SPA, an ASP.NET Core API, a .NET background worker for data ingestion, and PostgreSQL (external on Aiven). The ingestion worker and API are separate processes — this prevents long-running data sync jobs from blocking HTTP request handling and keeps deployment concerns clean. State management for multi-panel comparison UI fits well with Zustand (lightweight) or Redux Toolkit; panel state lives in the client, not the server.

---

## 1. Component Boundaries — What Talks to What

```
[External APIs]                [Aiven PostgreSQL]
  NBA Schedule API   ──────►  │                │
  Odds API (spreads) ──────►  │                │◄──── .NET API ◄──── React SPA
                              │                │                      (browser)
                   Ingestion  │   games        │   REST + JWT
                   Worker     │   teams        │   endpoints
                   (writes)   │   spreads      │   (reads, auth)
                              │   totals       │
                              │   users        │
                              └────────────────┘
```

**Four containers, two concern boundaries:**

| Container | Role | Talks To |
|-----------|------|----------|
| `api` — ASP.NET Core Web API | Serves REST endpoints, handles JWT auth, reads DB | PostgreSQL (Aiven), React (via HTTP) |
| `worker` — .NET Worker Service | Scheduled ingestion from external APIs, writes to DB | External NBA API, External Odds API, PostgreSQL |
| `frontend` — React (served via Nginx) | SPA served to browser, calls API | `api` container only |
| PostgreSQL | Persistent store | `api` (reads/writes), `worker` (writes) |

The API never calls external sports APIs directly. The worker never serves HTTP traffic. They share only the database.

---

## 2. Data Flow — How Information Moves

### Ingestion Flow (daily batch)

```
Cron trigger (daily, e.g. 6:00 AM)
  └─► Worker: fetch NBA schedule/scores for current season
        └─► Upsert games, teams into DB
  └─► Worker: fetch spreads + totals for each game date
        └─► Upsert spreads, totals, resolve ATS/O/U results
              (compare final score vs spread/total)
```

### Read Flow (user request)

```
Browser ──► React SPA ──► GET /api/teams (with JWT)
                    ──► .NET API ──► SELECT from teams, games, spreads
                                 ──► compute ATS%, O/U% in service layer
                    ◄── JSON response
              ◄── rendered team grid / panel
```

### Auth Flow

```
Browser ──► POST /api/auth/login (username + password)
        ◄── JWT access token (short-lived, e.g. 1 hour)
Browser stores token in memory or httpOnly cookie
All subsequent API requests: Authorization: Bearer <token>
API validates JWT on every protected endpoint — no session server state
```

---

## 3. Data Ingestion: Scheduled .NET Background Worker (Recommended)

**Recommendation: Separate .NET Worker Service container using `IHostedService` / `BackgroundService` with a timer loop or Quartz.NET.**

### Option Comparison

| Option | Pros | Cons |
|--------|------|------|
| `BackgroundService` in same API process | Simpler — one container | Ties ingestion lifecycle to API; long sync jobs block resources |
| Separate .NET Worker Service container | Clean separation, independent restarts, scalable | One extra container |
| External cron + script | Language-agnostic | Adds operational complexity, not .NET-native |
| Hangfire (in-process scheduler) | Dashboard, persistence, retries | Overkill for daily batch; adds DB tables |

**For this scale (daily batch, ~1,230 games/season), a separate `.NET Worker Service` project is the right call.** It uses the same `Microsoft.Extensions.Hosting` pattern as the API, shares model/data access libraries via a shared class library project, and runs independently. A `PeriodicTimer` (available in .NET 6+) or Quartz.NET handles the daily trigger.

### Worker Structure

```csharp
// Worker/Services/DailyIngestionWorker.cs
public class DailyIngestionWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (await timer.WaitForNextTickAsync(ct))
        {
            await _nbaIngestionService.SyncGamesAsync(ct);
            await _oddsIngestionService.SyncOddsAsync(ct);
        }
    }
}
```

The worker calls `INbaApiClient` and `IOddsApiClient` (typed `HttpClient` wrappers), writes to the DB via EF Core, and logs results. Retry logic belongs in the HTTP clients (Polly).

---

## 4. PostgreSQL Schema Sketch

Design principles: normalize teams and games; denormalize ATS/O/U result columns onto game records for fast reads; avoid recomputing results at query time.

### Tables

```sql
-- Teams
CREATE TABLE teams (
    id          SERIAL PRIMARY KEY,
    nba_api_id  VARCHAR(20) UNIQUE NOT NULL,   -- external API identifier
    name        VARCHAR(100) NOT NULL,           -- "Boston Celtics"
    abbreviation CHAR(3) NOT NULL,              -- "BOS"
    conference  VARCHAR(10),                    -- "East" / "West"
    division    VARCHAR(20),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Games (one row per game)
CREATE TABLE games (
    id              SERIAL PRIMARY KEY,
    nba_game_id     VARCHAR(30) UNIQUE NOT NULL,
    game_date       DATE NOT NULL,
    home_team_id    INT REFERENCES teams(id),
    away_team_id    INT REFERENCES teams(id),
    home_score      INT,                         -- null until game final
    away_score      INT,
    status          VARCHAR(20) NOT NULL,         -- 'scheduled','final','postponed'
    season          VARCHAR(10) NOT NULL,         -- '2024-25'
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Betting Lines (one row per game, updated as lines shift — store closing line)
CREATE TABLE game_lines (
    id              SERIAL PRIMARY KEY,
    game_id         INT UNIQUE REFERENCES games(id) ON DELETE CASCADE,
    spread          NUMERIC(5,1),               -- home team spread, e.g. -4.5
    total           NUMERIC(5,1),               -- o/u total, e.g. 224.5
    home_spread_odds INT,                       -- American odds, e.g. -110
    away_spread_odds INT,
    over_odds       INT,
    under_odds      INT,
    bookmaker       VARCHAR(50),                -- source bookmaker name
    line_timestamp  TIMESTAMPTZ,               -- when line was recorded
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- ATS and O/U Results (resolved after game final)
CREATE TABLE game_results (
    id              SERIAL PRIMARY KEY,
    game_id         INT UNIQUE REFERENCES games(id) ON DELETE CASCADE,
    home_covered    BOOLEAN,                    -- null if no line or push
    away_covered    BOOLEAN,
    ats_push        BOOLEAN NOT NULL DEFAULT false,
    over_hit        BOOLEAN,                    -- null if no total or push
    ou_push         BOOLEAN NOT NULL DEFAULT false,
    resolved_at     TIMESTAMPTZ
);

-- Users
CREATE TABLE users (
    id              SERIAL PRIMARY KEY,
    username        VARCHAR(50) UNIQUE NOT NULL,
    password_hash   VARCHAR(255) NOT NULL,      -- bcrypt
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_login_at   TIMESTAMPTZ
);
```

### Key Query Pattern (team ATS summary)

```sql
SELECT
    t.abbreviation,
    t.name,
    COUNT(*) FILTER (WHERE g.home_team_id = t.id AND gr.home_covered = true
                        OR g.away_team_id = t.id AND gr.away_covered = true) AS covers,
    COUNT(*) FILTER (WHERE gr.ats_push = false AND gr.home_covered IS NOT NULL) AS ats_games,
    COUNT(*) FILTER (WHERE gr.over_hit = true) AS overs,
    COUNT(*) FILTER (WHERE gr.over_hit = false) AS unders,
    COUNT(*) FILTER (WHERE gr.ou_push = true) AS ou_pushes
FROM teams t
JOIN games g ON (g.home_team_id = t.id OR g.away_team_id = t.id)
JOIN game_lines gl ON gl.game_id = g.id
JOIN game_results gr ON gr.game_id = g.id
WHERE g.season = '2024-25' AND g.status = 'final'
GROUP BY t.id;
```

Indexes to add: `games(season, status)`, `games(home_team_id)`, `games(away_team_id)`, `game_lines(game_id)`, `game_results(game_id)`.

---

## 5. .NET API Structure

### Project Layout

```
/src
  /NbaTracker.Api          ← ASP.NET Core Web API
    /Controllers
      AuthController.cs    ← POST /api/auth/login, /register
      TeamsController.cs   ← GET /api/teams, GET /api/teams/{id}/stats
      GamesController.cs   ← GET /api/games?teamId=&season=
    /Services
      ITeamStatsService.cs / TeamStatsService.cs
      IAuthService.cs / AuthService.cs
    /Models
      Dto/                 ← response shapes (TeamSummaryDto, GameResultDto)
    Program.cs
    appsettings.json

  /NbaTracker.Worker       ← .NET Worker Service
    /Services
      INbaApiClient.cs / NbaApiClient.cs
      IOddsApiClient.cs / OddsApiClient.cs
      DailyIngestionWorker.cs
    Program.cs

  /NbaTracker.Data         ← Shared class library (EF Core, models, migrations)
    /Entities              ← Team, Game, GameLine, GameResult, User
    /Repositories or DbContext
    NbaTrackerDbContext.cs
    Migrations/

/frontend                  ← React app (Vite)
```

### Endpoints Needed

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/auth/login` | Exchange credentials for JWT |
| POST | `/api/auth/register` | Create user (admin-only or invite) |
| GET | `/api/teams` | All 30 teams with ATS + O/U aggregate stats |
| GET | `/api/teams/{id}` | Single team detail |
| GET | `/api/teams/{id}/games` | Game log for a team with line + result |
| GET | `/api/games` | Games filtered by date, team, status |
| GET | `/api/sync/status` | Last ingestion run status (admin/debug) |

### Service Pattern

Controllers are thin — they validate input, call a service, return the DTO. Services contain all business logic (ATS computation, result resolution). The `NbaTracker.Data` shared library owns EF Core context and all entities; both `Api` and `Worker` reference it.

---

## 6. Docker Multi-Container Setup

### `docker-compose.yml` Structure

```yaml
version: "3.9"

services:
  api:
    build:
      context: ./src/NbaTracker.Api
      dockerfile: Dockerfile
    ports:
      - "5000:8080"
    environment:
      - ConnectionStrings__Default=${AIVEN_POSTGRES_CONN}
      - Jwt__Secret=${JWT_SECRET}
      - Jwt__Issuer=NbaTracker
    depends_on:
      - db        # only needed for local dev; prod points at Aiven
    networks:
      - backend

  worker:
    build:
      context: ./src/NbaTracker.Worker
      dockerfile: Dockerfile
    environment:
      - ConnectionStrings__Default=${AIVEN_POSTGRES_CONN}
      - NbaApi__Key=${NBA_API_KEY}
      - OddsApi__Key=${ODDS_API_KEY}
    depends_on:
      - db
    networks:
      - backend

  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile       # Vite build → Nginx serve
    ports:
      - "3000:80"
    environment:
      - VITE_API_BASE_URL=http://localhost:5000
    networks:
      - backend

  db:                               # local dev only — prod uses Aiven
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: nbatracker
      POSTGRES_USER: ${DB_USER}
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - pgdata:/var/lib/postgresql/data
    networks:
      - backend

volumes:
  pgdata:

networks:
  backend:
```

**Production note:** In production, the `db` service is removed from compose; all containers point `ConnectionStrings__Default` at the Aiven connection string. Secrets live in `.env` (not committed) or a secret manager.

### Frontend Dockerfile (Nginx pattern)

```dockerfile
# Stage 1: build
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# Stage 2: serve
FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

The `nginx.conf` should proxy `/api/*` requests to the `api` container to avoid CORS issues in production (single-origin setup).

---

## 7. React State Management for Multi-Panel Comparison

### Recommendation: Zustand + React Query

**Zustand** for panel/UI state. **React Query (TanStack Query)** for server data fetching and caching.

| Concern | Tool | Why |
|---------|------|-----|
| Which team panels are open | Zustand | Pure client UI state, no server involvement |
| Panel order / layout | Zustand | Ditto |
| Team stats data | React Query | Caches per-team, deduplicates parallel fetches |
| Auth token | Zustand (or React context) | Needs to be accessible across app |
| Sorting/filter prefs | Zustand | User preference, potentially persist to localStorage |

### Panel State Shape (Zustand)

```ts
interface PanelStore {
  openPanels: string[];          // team IDs in display order
  addPanel: (teamId: string) => void;
  removePanel: (teamId: string) => void;
  reorderPanels: (from: number, to: number) => void;
}
```

Each open panel independently calls `useQuery(['team', teamId], () => fetchTeamStats(teamId))`. React Query deduplicates identical calls and caches results. No Redux needed — this use case is not complex enough to warrant it.

**Do not** store fetched server data (team stats, game logs) in Zustand — that creates a manual cache that duplicates React Query's job. Zustand owns UI-only state; React Query owns server state.

---

## 8. Suggested Build Order

Dependencies between components determine a natural build sequence:

```
Phase 1 — Foundation
  1. Shared Data project: EF Core entities, DbContext, migrations
  2. PostgreSQL schema: apply migrations to local + Aiven
  3. Docker compose scaffold: wire up containers, confirm connectivity

Phase 2 — Ingestion Worker
  4. NBA API client + game/team ingestion (get real data into DB first)
  5. Odds API client + spread/total ingestion
  6. ATS/O/U result resolution logic (needs final scores + lines in DB)
  7. Scheduler wiring (daily trigger, logging)

Phase 3 — API
  8. Auth endpoints (JWT login/register) — needed before any protected routes
  9. Teams endpoint: aggregate ATS + O/U stats per team
  10. Games endpoint: team game log with lines + results

Phase 4 — Frontend
  11. Auth flow (login page, token storage, protected routes)
  12. Team grid (main page: all 30 teams, sortable columns)
  13. Panel system (open/close/reorder team panels)
  14. Team detail panel content (game log, ATS chart)

Phase 5 — Polish + Deploy
  15. Nginx reverse proxy config
  16. Production Docker compose (no local DB, Aiven conn string)
  17. Column customization (localStorage-persisted user prefs)
```

**Critical dependency:** The ingestion worker must run successfully and populate the DB before any meaningful API or frontend development can be tested against real data. Build and validate ingestion first.

---

## 9. Open Questions / Decisions Still Needed

| Question | Impact | Recommendation |
|----------|--------|---------------|
| Which NBA API (RapidAPI "NBA API Free Data" vs others)? | Ingestion client design, rate limits | Validate it covers 2024-25 scores + game IDs before building client |
| The Odds API vs SportsDataIO for spreads/totals? | Free tier limits matter (500 req/month on Odds API free tier — likely enough for daily batch on ~1,230 games if done efficiently) | The Odds API is fine if historical odds are pre-stored; verify historical odds endpoint availability |
| Closing line vs opening line storage? | Schema and ingestion timing | Store closing line (most predictive); capture once after game tips off |
| Will the `worker` and `api` share one .NET solution? | Monorepo vs separate repos | Single solution with three projects (Api, Worker, Data) is cleanest for this scale |
| Register endpoint — admin-only or self-serve? | Auth flow complexity | Admin-only (hardcoded invite) for small friend group; skip email verification |

---

## Artifacts Referenced

- Project requirements: `.planning/PROJECT.md`
- Tech stack: .NET 8 + ASP.NET Core, React (Vite), PostgreSQL 16, Docker Compose
- External data sources: RapidAPI NBA Free Data, The Odds API

---

*Research complete. Ready to inform phase structure in roadmap.*
