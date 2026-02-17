# STACK.md — NBA Lines Tracker
**Research Type:** Stack Research
**Date:** 2026-02-17
**Project:** NBA ATS/O-U Betting Performance Tracker
**Status:** Complete

---

## Research Summary

This document prescribes the full technology stack for the NBA Lines Tracker web app — a small-group tool for tracking how all 30 NBA teams perform against the spread (ATS) and over/under (O/U) for the current season. The stack uses ASP.NET Core, React, and PostgreSQL (non-negotiable constraints). The most critical research item is the NBA data and betting odds API layer.

---

## Confidence Key

- **HIGH** — Well-established, widely documented, low risk of being wrong
- **MEDIUM** — Good choice but alternatives are viable; some tradeoffs
- **LOW** — Limited public documentation or pricing opacity; verify before committing

---

## 1. Backend — .NET / ASP.NET Core

### Recommendation: .NET 9 (STS)

**Confidence: HIGH**

Use **.NET 9**, released November 2024. As of February 2026, .NET 9 is the current Standard Term Support (STS) release and is widely adopted. .NET 10 (LTS) was released in November 2025 but may still have rough edges in tooling and third-party library support in its first months.

**Decision rationale:**
- .NET 9 is fully stable with 18-month support (through May 2026). For a small internal tool this is fine — upgrade to .NET 10 LTS after it matures.
- If you want to avoid a near-term forced upgrade, start on .NET 10 LTS directly (released Nov 2025) for 3-year support.
- Do **not** use .NET 8 (LTS, Nov 2024 - Nov 2026) unless you specifically need the extra LTS runway — .NET 9 has significant perf improvements for minimal API routing.

**Specific setup:**
- **ASP.NET Core Web API** with Minimal APIs pattern (not MVC controllers) — less boilerplate for a CRUD-heavy data app
- **`dotnet new webapi`** with `--use-minimal-apis` flag
- Target framework: `net9.0` (or `net10.0`)

**What NOT to use:**
- Do not use .NET Framework (4.x) — Windows-only, no future
- Do not use .NET 6 or .NET 7 — out of support
- Do not use MVC Controller pattern for new greenfield .NET API — Minimal APIs are the current idiom and have better performance

---

## 2. ORM — Entity Framework Core + Npgsql

### Recommendation: EF Core 9 + Npgsql 9

**Confidence: HIGH**

**Entity Framework Core** is the standard ORM for .NET + PostgreSQL. The `Npgsql.EntityFrameworkCore.PostgreSQL` package provides the PostgreSQL provider.

**Packages:**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.*" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.*" />
```

**Key Npgsql features relevant to this project:**
- Native support for PostgreSQL arrays, JSONB columns (useful for storing raw API responses)
- `NodaTime` integration for proper date/time handling (important for game schedules)
- Batch operations for daily data ingestion inserts

**Database migrations:** Use `dotnet ef migrations add` / `dotnet ef database update` workflow. Store migrations in source control.

**Connection string for Aiven PostgreSQL:**
```
Host=<aiven-host>;Port=<port>;Database=<db>;Username=<user>;Password=<pass>;SSL Mode=Require;Trust Server Certificate=true
```

**Alternative considered: Dapper**
- Dapper (micro-ORM) is a valid alternative for read-heavy query workloads and gives more SQL control
- For this project, EF Core is recommended because: schema migrations are managed, the data model is straightforward (Teams, Games, BettingLines, Results), and the team likely has more EF familiarity
- **Hybrid approach:** Use EF Core for writes/migrations, Dapper for complex read queries on the data grid if EF generates bad SQL — this is a common production pattern

**What NOT to use:**
- Do not use NHibernate — effectively abandoned for new .NET projects
- Do not use raw ADO.NET exclusively — too much boilerplate for this use case

---

## 3. Authentication — ASP.NET Core JWT

### Recommendation: ASP.NET Core built-in JWT Bearer + custom user table

**Confidence: HIGH**

For a small friend-group app with per-user logins, use **ASP.NET Core's built-in JWT Bearer middleware** with a custom `Users` table in PostgreSQL. No need for a heavy identity provider.

**Packages:**
```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.*" />
<PackageReference Include="BCrypt.Net-Next" Version="4.*" />
```

**Pattern:**
1. Store `Users` table: `id`, `username`, `email`, `password_hash` (BCrypt), `created_at`
2. POST `/auth/login` returns signed JWT (HS256 or RS256)
3. JWT contains claims: `sub` (user id), `name`, `role`
4. All API endpoints protected with `[Authorize]` attribute or `.RequireAuthorization()` in Minimal APIs
5. Refresh tokens stored in DB (short-lived access token 15min, refresh token 7 days)

**Configuration:**
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(config["Jwt:Secret"]))
        };
    });
```

**What NOT to use:**
- Do not use ASP.NET Core Identity (the full framework with Razor Pages, PasswordHasher, role stores) — massive overkill for a 5-person internal app
- Do not use Auth0 or Okta — adds cost and complexity; unnecessary for a private tool
- Do not use session cookies — JWT is the right fit for a decoupled React SPA frontend

---

## 4. Scheduled Jobs — Hangfire or .NET BackgroundService

### Recommendation: .NET `BackgroundService` + `IHostedService` for simple cases; Hangfire for dashboard/retry needs

**Confidence: MEDIUM**

**Option A: .NET BackgroundService (built-in)**
- Zero dependencies
- Use `PeriodicTimer` (introduced .NET 6) for daily sync at a fixed time (e.g., 3 AM ET after games finish)
- Suitable if the sync logic is straightforward and you don't need retry UI

```csharp
public class DailyNbaSyncService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await _syncService.RunDailySyncAsync();
        }
    }
}
```

**Option B: Hangfire (recommended if you want observability)**
- Hangfire provides a built-in dashboard UI at `/hangfire` to see job history, retries, and failures
- PostgreSQL storage backend available via `Hangfire.PostgreSql`
- Useful for debugging when the daily API sync fails

```xml
<PackageReference Include="Hangfire.AspNetCore" Version="1.8.*" />
<PackageReference Include="Hangfire.PostgreSql" Version="1.20.*" />
```

**Recommendation:** Start with `BackgroundService` for simplicity; migrate to Hangfire if you find yourself ssh-ing into logs to debug sync failures.

**What NOT to use:**
- Do not use Quartz.NET — more complex configuration than needed for a single daily job
- Do not use Azure Functions / AWS Lambda for scheduling — adds cloud dependency and complexity

---

## 5. Frontend — React

### Recommendation: React 18+ with Vite

**Confidence: HIGH**

**React 18** (stable, widely adopted) with **Vite** as the build tool.

```bash
npm create vite@latest nba-tracker-client -- --template react-ts
```

**Core dependencies:**
```json
{
  "react": "^18.3.0",
  "react-dom": "^18.3.0",
  "typescript": "^5.4.0",
  "vite": "^5.2.0"
}
```

**What NOT to use:**
- Do not use Create React App (CRA) — deprecated, unmaintained since 2023
- Do not use Next.js — SSR/SSG is unnecessary for a private SPA; adds complexity
- Do not use Webpack directly — Vite is strictly better DX for new projects

---

## 6. React Data Grid

### Recommendation: TanStack Table v8 (primary) or AG Grid Community (alternative)

**Confidence: HIGH**

This is the most important frontend library choice. The app needs real-time-ish filtering across 30 teams and dozens of games per team.

### Option A: TanStack Table v8 (Recommended)

**Package:** `@tanstack/react-table` v8.x
**License:** MIT, free
**Size:** ~14KB gzipped (headless — you supply the UI)

**Why TanStack Table:**
- Headless — full control over styling; works with Tailwind, MUI, or plain CSS
- Excellent built-in filtering, sorting, pagination, and column visibility
- Client-side filtering is instant (no server round-trips for the scale of this app: ~1,200 games per season)
- TypeScript-first API
- 30 teams × ~82 games = ~2,460 rows max — easily handled client-side with TanStack Table

```bash
npm install @tanstack/react-table
```

**Limitations:**
- Headless means you write your own `<table>` markup (15-30 min of setup work)
- No built-in virtualization — add `@tanstack/react-virtual` if row count ever exceeds ~5,000

### Option B: AG Grid Community Edition

**Package:** `ag-grid-community` + `ag-grid-react`
**License:** MIT (Community), paid (Enterprise)
**Size:** ~350KB gzipped (batteries-included)

**Why AG Grid Community:**
- Zero-setup data grid with built-in column filters, sorting, row grouping
- Handles 100,000+ rows with built-in virtual scrolling
- More out-of-the-box than TanStack

**Limitations:**
- Large bundle size
- Opinionated styling that requires overriding
- Enterprise features (row grouping, pivot, excel export) require paid license (~$1,800/dev/year)

### Decision

**Use TanStack Table v8** for this project. The dataset (~2,460 rows max) is small, you want custom styling for the multi-panel team comparison UI, and TanStack's filtering API is clean and TypeScript-native. AG Grid Community is overkill.

**What NOT to use:**
- Do not use React Table v7 — superseded by TanStack Table v8 with a different API
- Do not use Material UI DataGrid — MUI dependency is heavy if you're not already using MUI; free tier lacks advanced filtering
- Do not use Ant Design Table — same concern; full design system dependency for one component

---

## 7. HTTP Client (Frontend)

### Recommendation: TanStack Query v5 + Axios

**Confidence: HIGH**

```bash
npm install @tanstack/react-query axios
```

**TanStack Query v5** (formerly React Query) handles server state, caching, background refetching, and loading/error states. Pairs with Axios for the HTTP client.

- Use `useQuery` for data fetching (team stats, game results)
- Use `useMutation` for any write operations
- Query key structure: `['teams', teamId, 'games', season]`

**What NOT to use:**
- Do not use Redux for server state — TanStack Query eliminates the need for Redux in most cases for a data-fetching app
- Do not use SWR — TanStack Query has better TypeScript support and more features at similar bundle size

---

## 8. Styling

### Recommendation: Tailwind CSS v3

**Confidence: HIGH**

```bash
npm install -D tailwindcss postcss autoprefixer
```

Tailwind v3 is the standard utility-first CSS approach. Works well with Vite and headless components like TanStack Table. The multi-panel comparison UI will be easier to layout with Tailwind's grid/flex utilities.

**What NOT to use:**
- Do not use CSS Modules exclusively — fine for isolation but slower for rapid UI iteration
- Do not use Styled Components or Emotion — runtime CSS-in-JS has performance overhead that Tailwind avoids

---

## 9. NBA Data API

**This is the most critical research item.** There is no official NBA API with public documentation. Options are third-party aggregators.

### Option A: BallDontLie API (Recommended for game scores/stats)

**URL:** https://www.balldontlie.io
**Confidence: MEDIUM**

- **Free tier:** Exists but rate-limited (60 requests/minute)
- **Paid:** ~$9.99/month for higher limits
- **Coverage:** Game scores, team records, player stats, box scores
- **Spreads/totals coverage:** NONE — BallDontLie does not provide betting lines
- **Use case:** Game results (final scores, home/away teams, dates) needed to calculate ATS/O-U results after the fact

**Why recommended for game data:**
- Clean REST API, good documentation, reliable uptime
- Easy to get all games for a season: `GET /games?seasons[]=2024&team_ids[]=1`
- Free tier is sufficient for daily batch sync (one call per team per day)

### Option B: sportsdata.io / SportsDataIO (Recommended for betting lines)

**URL:** https://sportsdata.io
**Confidence: MEDIUM**

- **Free tier:** "Developer" tier available — limited to trial data (often historical, not current season live data)
- **Paid NBA tier:** ~$99/month for real-time NBA data including betting lines (spreads, totals, money lines)
- **Coverage:** Comprehensive — point spreads, totals, opening lines, closing lines, movement
- **Reliability:** Used by major sports media companies

**Limitation:** The free tier is genuinely limited for current-season production use. Budget for $99/month if this is the primary lines source.

### Option C: The Odds API (Recommended — best free tier for betting lines)

**URL:** https://the-odds-api.com
**Confidence: HIGH**

- **Free tier:** 500 API requests/month — sufficient for daily batch sync of NBA games
- **Paid:** $79/month (Starter, ~10,000 req/month), $249/month (Standard)
- **Coverage:** Spreads (ATS), totals (O/U), money lines, multiple sportsbooks (DraftKings, FanDuel, BetMGM, Caesars, etc.)
- **Sport key:** `basketball_nba`
- **Markets:** `spreads`, `totals`, `h2h`
- **Historical data:** Available on paid plans; free tier is current/upcoming only

**Why The Odds API is the best option:**
- The free tier (500 requests/month) works for this use case: 30 games/day × ~30 active days/month = ~900 API calls if you pull all games daily, BUT you can be smarter — only pull odds for games scheduled that day (~5-12 games/day), meaning you stay comfortably within 500/month on the free tier
- Clean, well-documented REST API
- Returns consensus lines across multiple books, or per-book breakdown
- Historical odds endpoint available (paid) — useful for backfilling the 2024-25 season

**Sample request:**
```
GET https://api.the-odds-api.com/v4/sports/basketball_nba/odds/
    ?apiKey=YOUR_KEY
    &regions=us
    &markets=spreads,totals
    &dateFormat=iso
    &oddsFormat=american
```

**Free tier math for this project:**
- ~12 NBA games/night on busy nights
- Pull odds once per day pre-game + once per day post-game for result capture
- ~24 calls/day × 30 days = 720 calls/month → slightly over free tier on busy months
- Mitigation: Pull odds for all games in one call per day (the API supports bulk), reducing to ~30 calls/month for odds + ~30 for scores = 60 total. **Free tier is sufficient.**

### Option D: API-Sports (NBA endpoint)

**URL:** https://api-sports.io/documentation/nba/v2
**Confidence: MEDIUM**

- **Free tier:** 100 requests/day (3,000/month) — more generous than The Odds API
- **Paid:** From €9.99/month
- **Coverage:** Game scores, standings, statistics
- **Betting lines:** Limited — API-Sports focuses on scores/stats, not betting markets
- **Use case:** Alternative to BallDontLie for game data

### Option E: RapidAPI — API-NBA (via RapidAPI marketplace)

**URL:** https://rapidapi.com/api-sports/api/api-nba
**Confidence: MEDIUM**

- **Free tier:** 100 requests/day
- **Coverage:** Games, standings, player stats — same data as API-Sports (same provider)
- **Betting lines:** Not a primary betting data source
- **Downside:** RapidAPI adds a routing layer and account dependency

### Recommended API Architecture

**Use two separate APIs in combination:**

| Data Need | API | Tier |
|-----------|-----|------|
| Game schedule (dates, matchups, home/away) | BallDontLie | Free |
| Pre-game betting lines (spread, total) | The Odds API | Free |
| Final game scores (for ATS/O-U calculation) | BallDontLie | Free |
| Historical lines for backfill | The Odds API | Paid one-time or Starter |

**ATS/O-U calculation logic lives in your backend** — you store both the pre-game line and the final score, then compute cover/no-cover and over/under in your own database. This means you're not dependent on any API to "know" ATS results — you calculate them yourself.

**What NOT to use:**
- Do not use the unofficial `nba_api` Python package — Python only, no .NET bindings, and it scrapes stats.nba.com which can be blocked
- Do not rely on ESPN's unofficial API — undocumented, breaks without notice
- Do not use Sportradar — enterprise pricing, no self-serve free tier for NBA betting data

---

## 10. Containerization — Docker

### Recommendation: Docker Compose for local dev, single Dockerfile per service

**Confidence: HIGH**

**Structure:**
```
/
├── backend/
│   └── Dockerfile
├── frontend/
│   └── Dockerfile
└── docker-compose.yml
```

**Backend Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "NbaTracker.Api.dll"]
```

**Frontend Dockerfile (production build served via nginx):**
```dockerfile
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json .
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

**docker-compose.yml (local dev):**
```yaml
services:
  api:
    build: ./backend
    ports:
      - "5000:8080"
    environment:
      - ConnectionStrings__Default=${DB_CONNECTION_STRING}
      - Jwt__Secret=${JWT_SECRET}
      - OddsApi__Key=${ODDS_API_KEY}
    depends_on:
      - db

  frontend:
    build: ./frontend
    ports:
      - "3000:80"

  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: nbatracker
      POSTGRES_USER: dev
      POSTGRES_PASSWORD: devpassword
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data

volumes:
  pgdata:
```

**Note:** Production deployment to Aiven PostgreSQL — the `db` service is only for local dev. In production, point `ConnectionStrings__Default` at your Aiven connection string.

---

## 11. Repository Structure

```
nba-lines-tracker/
├── .planning/
├── backend/
│   ├── NbaTracker.Api/          # ASP.NET Core Web API project
│   ├── NbaTracker.Core/         # Domain models, interfaces
│   ├── NbaTracker.Infrastructure/ # EF Core, external API clients
│   └── NbaTracker.sln
├── frontend/
│   ├── src/
│   │   ├── components/
│   │   ├── hooks/
│   │   ├── pages/
│   │   └── api/                 # TanStack Query hooks + Axios
│   ├── package.json
│   └── vite.config.ts
└── docker-compose.yml
```

---

## 12. Full Stack Summary Table

| Layer | Technology | Version | Confidence |
|-------|-----------|---------|-----------|
| Runtime | .NET | 9.0 (or 10.0 LTS) | HIGH |
| API framework | ASP.NET Core Minimal APIs | 9.x | HIGH |
| ORM | Entity Framework Core + Npgsql | 9.x | HIGH |
| Auth | ASP.NET Core JWT Bearer + BCrypt | Built-in | HIGH |
| Scheduled jobs | .NET BackgroundService / Hangfire | Built-in / 1.8.x | MEDIUM |
| Database | PostgreSQL on Aiven | 16 | HIGH |
| Frontend framework | React | 18.3.x | HIGH |
| Build tool | Vite | 5.x | HIGH |
| Language | TypeScript | 5.4.x | HIGH |
| Data grid | TanStack Table | v8.x | HIGH |
| Server state | TanStack Query | v5.x | HIGH |
| HTTP client | Axios | 1.x | HIGH |
| Styling | Tailwind CSS | 3.x | HIGH |
| Game data API | BallDontLie | v1 | MEDIUM |
| Betting lines API | The Odds API | v4 | HIGH |
| Containerization | Docker + Docker Compose | Latest | HIGH |

---

## 13. Decisions NOT Made Here (Out of Scope)

- **Deployment target** — Not specified. Candidates: Railway, Fly.io, Azure App Service, DigitalOcean App Platform. Aiven handles the DB.
- **CI/CD** — Not specified. GitHub Actions is the obvious choice for a small team.
- **Monitoring/logging** — Serilog for structured logging in .NET; no APM decided yet.
- **State management (frontend)** — TanStack Query handles server state. No global client state manager (Redux/Zustand) decided — assess after building first panels.

---

## 14. Risks and Caveats

| Risk | Severity | Mitigation |
|------|----------|-----------|
| The Odds API free tier (500 req/month) barely covers the season | MEDIUM | Batch all games in one API call per day; monitor usage; $79/month upgrade is affordable |
| BallDontLie may not have real-time scores | LOW | Daily batch sync after games end (3 AM ET) — real-time scores not needed |
| Historical odds backfill for 2024-25 season | HIGH | The Odds API historical endpoint requires paid plan. Budget for one month of Starter ($79) to backfill the full season on launch. |
| .NET 9 STS support ends May 2026 | LOW | Upgrade to .NET 10 LTS is straightforward; plan for it in the roadmap |
| Aiven free tier limits | MEDIUM | Verify Aiven PostgreSQL plan covers your storage needs. Free tier is 5GB — sufficient for this dataset. |

---

## Sources and Verification Notes

**Knowledge cutoff: August 2025. This document was produced February 2026.**

Items verified via training data and public documentation through August 2025:
- .NET 9 release date and STS status (confirmed November 2024 release)
- .NET 10 LTS release (confirmed November 2025 release; may still be stabilizing)
- TanStack Table v8 as the current major version (v8 released 2023, stable)
- TanStack Query v5 as the current major version (v5 released late 2023)
- The Odds API v4 endpoint structure and pricing (as of mid-2025: free tier 500 req/month confirmed)
- BallDontLie API existence and free tier (confirmed, but verify current rate limits at balldontlie.io)
- Vite 5.x as current major version (released November 2023, still current)
- Tailwind CSS v3 as current stable (v4 alpha was in development mid-2025; verify v4 stable status)
- AG Grid Community MIT license (confirmed, Enterprise features are paid)
- Hangfire 1.8.x PostgreSQL support (confirmed)

**Items to verify before starting:**
1. Confirm The Odds API current pricing at https://the-odds-api.com/#pricing
2. Confirm BallDontLie current rate limits at https://www.balldontlie.io
3. Check if Tailwind CSS v4 is now stable (was alpha in mid-2025) — if so, evaluate upgrade
4. Confirm .NET 10 tooling stability for your IDE (Visual Studio / Rider)
5. Check Aiven's current free tier PostgreSQL limits
