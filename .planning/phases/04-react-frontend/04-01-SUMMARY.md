---
phase: 04-react-frontend
plan: 01
subsystem: ui
tags: [react, tailwind, shadcn, tanstack-query, zustand, axios, jwt, react-router, typescript, vite]

# Dependency graph
requires:
  - phase: 03-rest-api
    provides: Auth endpoints (POST /api/auth/login, /api/auth/refresh, /api/auth/logout) and team endpoints (GET /api/teams, /api/teams/{id}/stats, /api/teams/{id}/games)

provides:
  - Backend: TeamStatsResponse record extended with Streak (int) and LastSyncedAt (string?) fields
  - Frontend: Vite + React + Tailwind v4 + shadcn/ui project scaffolded and building
  - Frontend: TypeScript types matching Phase 3 API shapes + streak + lastSyncedAt
  - Frontend: Axios client with JWT request interceptor and 401 refresh queue
  - Frontend: Zustand store with openPanels, isAuthenticated, theme (only theme persisted)
  - Frontend: React Router with ProtectedRoute/PublicRoute and session restore on mount
  - Frontend: Login page with shadcn/ui components and error handling
  - Frontend: TanStack Query hooks for all three team endpoints
  - Frontend: Utility libs: getPercentageColor, computeStreakFromGames, useLocalStorage

affects: [04-02, 04-03, 04-04]

# Tech tracking
tech-stack:
  added:
    - tailwindcss@4 + @tailwindcss/vite (CSS-first, no tailwind.config.js)
    - shadcn/ui (button, input, table, dropdown-menu, badge, skeleton, scroll-area, card, separator, label, alert)
    - @tanstack/react-query + @tanstack/react-table
    - react-router-dom
    - axios (with JWT request/response interceptors)
    - zustand + zustand/middleware (persist)
    - date-fns
    - @types/node
  patterns:
    - Access token in module memory (not localStorage) for XSS mitigation; refresh token in localStorage for persistence
    - Axios 401 interceptor with in-flight queue: concurrent requests queued during refresh, replayed after success
    - Zustand persist with partialize: only theme persisted, auth/panels reset on reload
    - React Router createBrowserRouter with ProtectedRoute (Outlet or Navigate) and PublicRoute patterns
    - TanStack Query hooks with enabled: param for conditional fetching (teamId !== null)
    - Tailwind v4 CSS-first config: @import "tailwindcss" in index.css, no config file needed

key-files:
  created:
    - nba-lines-tracker/frontend/src/types/api.ts
    - nba-lines-tracker/frontend/src/api/client.ts
    - nba-lines-tracker/frontend/src/api/auth.ts
    - nba-lines-tracker/frontend/src/api/teams.ts
    - nba-lines-tracker/frontend/src/store/useAppStore.ts
    - nba-lines-tracker/frontend/src/hooks/useLocalStorage.ts
    - nba-lines-tracker/frontend/src/lib/color.ts
    - nba-lines-tracker/frontend/src/lib/streak.ts
    - nba-lines-tracker/frontend/src/components/auth/LoginPage.tsx
    - nba-lines-tracker/frontend/src/components/ui/ (11 shadcn components)
    - nba-lines-tracker/frontend/src/index.css (Tailwind v4 CSS-first + shadcn CSS variables)
    - nba-lines-tracker/frontend/src/lib/utils.ts (shadcn cn helper)
  modified:
    - nba-lines-tracker/src/NbaTracker.Api/Models/TeamModels.cs
    - nba-lines-tracker/src/NbaTracker.Api/Endpoints/TeamEndpoints.cs
    - nba-lines-tracker/frontend/src/App.tsx
    - nba-lines-tracker/frontend/src/main.tsx
    - nba-lines-tracker/frontend/vite.config.ts
    - nba-lines-tracker/frontend/tsconfig.json
    - nba-lines-tracker/frontend/package.json

key-decisions:
  - "shadcn init --defaults: no style/base-color prompts needed with Tailwind v4 path; CSS variables use oklch color space (not HSL) in generated index.css"
  - "streak.ts computeStreakFromGames: removed unused teamId param per plan note — isHomeGame on GameLogEntry already encodes team perspective"
  - "formatStreak in types/api.ts returns dash for 0 streak; computeStreakFromGames in lib/streak.ts returns null for empty game arrays — two separate representations for grid vs panel"

patterns-established:
  - "JWT in-memory + localStorage split: access token in module-level variable (cleared on page reload), refresh token in localStorage (survives reload)"
  - "Axios 401 queue pattern: isRefreshing flag + failedQueue array prevents thundering herd on token expiry"
  - "Zustand partialize: persist only UI state (theme), never auth state — auth restored via tryRestoreSession on mount"

requirements-completed: [TEAM-01, TEAM-02, TEAM-03, TEAM-04, GRID-06]

# Metrics
duration: 5min
completed: 2026-02-20
---

# Phase 4 Plan 01: Application Foundation and Auth Shell Summary

**React + Tailwind v4 + shadcn/ui scaffold with JWT auth interceptor, Zustand store, TanStack Query hooks, protected routing, and backend Streak/LastSyncedAt fields added to GET /api/teams**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-02-20T22:18:03Z
- **Completed:** 2026-02-20T22:23:15Z
- **Tasks:** 3
- **Files modified:** 21 (6 modified, 15 created)

## Accomplishments

- Extended C# TeamStatsResponse record with Streak (int) and LastSyncedAt (string?) and updated GetAllTeamsAsync to compute per-team streak and fetch last sync timestamp
- Scaffolded complete Tailwind v4 + shadcn/ui frontend with 11 UI components, path aliases, and Vite /api proxy
- Built full auth layer: Axios client with JWT request interceptor + 401 refresh queue, tryRestoreSession on mount, ProtectedRoute/PublicRoute, and login page
- All TypeScript types match Phase 3 API shapes with new streak/lastSyncedAt fields
- npm run build succeeds: 258 modules, 0 TypeScript errors, 0 build errors

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend TeamStatsResponse with Streak and LastSyncedAt** - `e2bad7a` (feat)
2. **Task 2: Install dependencies, configure Tailwind v4 + shadcn/ui, update Vite config** - `dfab4f5` (chore)
3. **Task 3: Build app shell — types, API layer, Zustand store, utilities, auth, routing, login** - `a4ede85` (feat)

## Files Created/Modified

- `nba-lines-tracker/src/NbaTracker.Api/Models/TeamModels.cs` - Added Streak (int) and LastSyncedAt (string?) to TeamStatsResponse record
- `nba-lines-tracker/src/NbaTracker.Api/Endpoints/TeamEndpoints.cs` - Added streak computation loop and SyncRuns lastSyncedAt query to GetAllTeamsAsync
- `nba-lines-tracker/frontend/vite.config.ts` - Tailwind v4 plugin, @ alias, /api proxy to localhost:5000
- `nba-lines-tracker/frontend/tsconfig.json` - Added baseUrl and paths for @ alias
- `nba-lines-tracker/frontend/src/index.css` - Tailwind v4 CSS-first config with shadcn CSS variables (oklch)
- `nba-lines-tracker/frontend/src/types/api.ts` - TeamStatsResponse, HomeAwaySplit, TeamDetailResponse, GameLogEntry interfaces + calcAtsPct/calcOuPct/formatStreak helpers
- `nba-lines-tracker/frontend/src/api/client.ts` - Axios instance with Bearer token interceptor and 401 refresh queue
- `nba-lines-tracker/frontend/src/api/auth.ts` - login, logout, tryRestoreSession
- `nba-lines-tracker/frontend/src/api/teams.ts` - useTeams, useTeamStats, useTeamGames TanStack Query hooks
- `nba-lines-tracker/frontend/src/store/useAppStore.ts` - Zustand store: theme + isAuthenticated + openPanels; only theme persisted
- `nba-lines-tracker/frontend/src/hooks/useLocalStorage.ts` - Generic typed localStorage hook
- `nba-lines-tracker/frontend/src/lib/color.ts` - getPercentageColor returning HSL inline style
- `nba-lines-tracker/frontend/src/lib/streak.ts` - computeStreakFromGames from GameLogEntry[]
- `nba-lines-tracker/frontend/src/components/auth/LoginPage.tsx` - Login form with shadcn Button/Input/Label/Alert
- `nba-lines-tracker/frontend/src/App.tsx` - createBrowserRouter with ProtectedRoute/PublicRoute, tryRestoreSession on mount
- `nba-lines-tracker/frontend/src/main.tsx` - QueryClientProvider wrapping App
- `nba-lines-tracker/frontend/src/lib/utils.ts` - shadcn cn() helper (generated by shadcn init)
- `nba-lines-tracker/frontend/src/components/ui/` - 11 shadcn components (button, input, table, dropdown-menu, badge, skeleton, scroll-area, card, separator, label, alert)

## Decisions Made

- shadcn init with `--defaults` works with Tailwind v4 when index.css already contains `@import "tailwindcss"` — the init detects v4 and writes CSS variables in oklch color space rather than HSL
- Removed unused `teamId` parameter from `computeStreakFromGames` — the plan's NOTE said to remove it since `GameLogEntry.isHomeGame` already encodes the team's perspective; TypeScript strict mode would have flagged it
- Two streak representations: `formatStreak(streak: number)` in types/api.ts for grid display (uses the backend's precomputed integer); `computeStreakFromGames(games)` in lib/streak.ts for panel display (recomputes from game log)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Created index.css before shadcn init**
- **Found during:** Task 2 (shadcn init)
- **Issue:** shadcn init failed with "No Tailwind CSS configuration found" — it requires an existing CSS file with `@import "tailwindcss"` to detect v4
- **Fix:** Created `src/index.css` with `@import "tailwindcss"` before running `npx shadcn@latest init --defaults`; shadcn then detected v4 and appended its CSS variable block
- **Files modified:** nba-lines-tracker/frontend/src/index.css
- **Verification:** shadcn init succeeded, index.css now contains `@import "tailwindcss"` (first line) plus shadcn CSS variables
- **Committed in:** dfab4f5 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 3 - blocking)
**Impact on plan:** Required pre-creating index.css before shadcn init; final output matches plan spec exactly.

## Issues Encountered

- dotnet build used wrong solution filename — plan referenced `nba-lines-tracker.sln` but actual file is `NbaTracker.sln`. Fixed immediately by checking `ls`.

## User Setup Required

None — no external service configuration required for this plan.

## Next Phase Readiness

- Backend: GET /api/teams now returns `streak` and `lastSyncedAt` in response
- Frontend: Auth layer, routing, types, and utility functions all in place for Plan 04-02 (team grid component)
- Plan 04-02 can import from `@/api/teams`, `@/store/useAppStore`, `@/lib/color`, `@/types/api` immediately
- No blockers — build is clean, types compile, shadcn components are available

---
*Phase: 04-react-frontend*
*Completed: 2026-02-20*
