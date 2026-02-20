# Phase 4: React Frontend - Research

**Researched:** 2026-02-20
**Domain:** React 18 + TypeScript + Vite + Tailwind CSS + shadcn/ui
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### Visual theme & styling
- Both dark mode and light mode — user-controlled toggle
- Tailwind CSS for all styling
- shadcn/ui component library (Radix UI primitives, accessible, copy-paste, no version lock-in)
- Clean / minimal aesthetic — white space, subtle borders, data speaks for itself (think Linear or Notion)

#### Grid layout & defaults
- Default visible columns: Team, W-L record, Current streak, ATS%, ATS record (covers/misses/pushes), O/U%, O/U record (overs/unders/pushes)
- Default sort order: ATS% descending — best cover teams appear at top
- Column visibility preference persists to localStorage (survives page refresh and new sessions)
- Clicking anywhere on a team row opens the detail panel — no separate button needed

#### Multi-panel behavior
- No hard limit on panels — panels appear below the grid and scroll horizontally as more open
- Panels render in a horizontal strip below the full-width grid
- If a user clicks a team already open in a panel: scroll to and highlight the existing panel (no duplicate panels)
- Close via X button in the panel header only

#### Color coding & thresholds
- ATS% threshold: 50% exactly — above 50% is green, below 50% is red
- Color intensity uses a gradient — further from 50% = stronger green or red (not binary)
- O/U% uses the same color logic: above 50% overs hit = green, below = red, same gradient intensity
- Push results are neutral — pushes do not shift color toward green or red in either ATS or O/U display

### Claude's Discretion
- Exact gradient implementation (CSS custom properties, Tailwind JIT values, or inline style calculation)
- Specific shade values for green/red at various percentage distances from 50%
- Loading skeleton design and empty/error states
- Exact spacing, typography scale, and border radius choices within the "clean/minimal" aesthetic
- Dark mode color palette specifics (surface colors, border colors, text hierarchy)

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| TEAM-01 | User can view all 30 NBA teams with current season W-L record | GET /api/teams returns `wins`/`losses`/`gamesPlayed`; TanStack Query fetches and caches; TanStack Table renders |
| TEAM-02 | User can view current game streak per team (W or L with count) | Streak is NOT in the TeamStatsResponse DTO — must be computed from GET /api/teams/{id}/games game log OR added to the /api/teams response (see Open Questions) |
| TEAM-03 | User can view ATS stats per team (covered count, missed count, push count, cover %) | GET /api/teams returns `atsCovers`, `atsLosses`, `atsPushes`; ATS% computed as `atsCovers / (atsCovers + atsLosses)` (pushes excluded) |
| TEAM-04 | User can view O/U stats per team (overs hit, unders hit, push count, hit %) | GET /api/teams returns `ouOvers`, `ouUnders`, `ouPushes`; O/U% = `ouOvers / (ouOvers + ouUnders)` |
| TEAM-05 | User can view last 10 games ATS and O/U stats per team | Computed from first 10 entries in GET /api/teams/{id}/games (already sorted desc by date) |
| TEAM-06 | User can view home split ATS and O/U stats per team | GET /api/teams/{id}/stats returns `home` HomeAwaySplit object |
| TEAM-07 | User can view away split ATS and O/U stats per team | GET /api/teams/{id}/stats returns `away` HomeAwaySplit object |
| GRID-01 | User can sort the team grid by any displayed column | TanStack Table `getSortedRowModel()` + `onSortingChange` state handler |
| GRID-02 | User can filter teams by conference (East / West) | TanStack Table column filter on `conference` field; shadcn/ui DropdownMenu or ToggleGroup for filter UI |
| GRID-03 | User can filter teams by division | TanStack Table column filter on `division` field; shadcn/ui Select or DropdownMenu |
| GRID-04 | User can show or hide columns in the grid (customizable per user) | TanStack Table `VisibilityState` + `onColumnVisibilityChange`; persist to localStorage |
| GRID-05 | User can see color-coded indicators on ATS% (green above threshold, red below) | Gradient color function: `getPercentageColor(value, 50)` returns inline style `backgroundColor`; intensity scales with distance from 50% |
| GRID-06 | User can see when data was last synced ("Last synced: X ago") | Fetch GET /api/admin/sync-status (last run) or add `lastSynced` to GET /api/teams response header or body; display with `formatDistanceToNow` from date-fns |
| PANEL-01 | User can open a team detail panel by clicking a team in the grid | `onRowClick` handler adds teamId to `openPanels` array in Zustand store |
| PANEL-02 | User can have multiple team panels open simultaneously for side-by-side comparison | `openPanels` is an ordered array; each panel fetches its own data via TanStack Query |
| PANEL-03 | User can open additional team panels without closing existing ones | Pushing to `openPanels` array without clearing; duplicate check scrolls instead of adding |
| PANEL-04 | Each panel displays the team's full summary stats (ATS%, O/U%, record, streak, home/away splits) | Composed from GET /api/teams/{id}/stats response; streak computed in-panel from GET /api/teams/{id}/games |
| PANEL-05 | Each panel displays a game-by-game log (opponent, game result, spread line, ATS result, total line, O/U result) | GET /api/teams/{id}/games returns `GameLogEntry[]` with all needed fields |
</phase_requirements>

---

## Summary

The frontend is a React 18 + TypeScript SPA built with Vite, already scaffolded from Phase 1. The project has a placeholder App.tsx that needs to be replaced with the full application. The stack is Tailwind CSS + shadcn/ui (Radix UI primitives), with all visual decisions locked by the user. The two most important libraries to add are TanStack Query v5 (server state management, caching, loading states) and TanStack Table v8 (headless sortable/filterable/column-visibility table). shadcn/ui components (Table, ScrollArea, DropdownMenu, Badge, Skeleton, Button, Input) handle all presentational primitives.

The application has three screens: Login (unauthenticated), Main (team grid + panel strip), and there is no other routing needed since this is a single-view data application. Client-side routing with React Router v7 (library mode) handles the login/main page split and protects the main route. JWT tokens (15-minute access + 7-day refresh) require an Axios interceptor pattern that transparently refreshes the access token and retries the original request on 401 response.

A critical open question affects TEAM-02 and PANEL-04: the current `GET /api/teams` response does not include a streak field. Computing streak requires either (a) adding it to the API response (preferred, avoids 30 extra game-log requests on page load) or (b) computing it from the panel's own game log request (acceptable for panels but not for the grid column). The planner must decide this before task breakdown.

**Primary recommendation:** Add a `Streak` field to `TeamStatsResponse` on the API side (a single number, positive = win streak, negative = loss streak), computed from the already-loaded game data in `GetAllTeamsAsync`. This avoids 30 extra HTTP requests and keeps the grid data complete in one call.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| react | 18.3.1 (already installed) | UI framework | Already in project |
| react-dom | 18.3.1 (already installed) | DOM rendering | Already in project |
| typescript | 5.5.4 (already installed) | Type safety | Already in project |
| vite | 5.4.2 (already installed) | Build tool | Already in project |
| tailwindcss | 4.x (via @tailwindcss/vite) | Utility CSS | Locked decision |
| shadcn/ui | latest CLI | Component library | Locked decision |
| @tanstack/react-query | ^5.90.21 | Server state, caching | Industry standard for REST APIs |
| @tanstack/react-table | ^8.21.3 | Headless table | shadcn/ui data table is built on it |
| react-router-dom | ^7.x | Client routing | SPA login protection |
| axios | ^1.x | HTTP client with interceptors | JWT refresh interceptor pattern |
| zustand | ^5.x | Client state (panels, auth, theme) | Lightweight, no re-render flooding |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| date-fns | ^3.x | Date formatting | "Last synced X ago" display, game log dates |
| @types/node | latest | Node types for Vite config | Required for `path.resolve()` in vite.config.ts |

### shadcn/ui Components to Add
These are copy-pasted into the project — not versioned npm packages:

| Component | Command | Used For |
|-----------|---------|---------|
| button | `npx shadcn@latest add button` | Login submit, panel close, filter toggles |
| input | `npx shadcn@latest add input` | Login email/password fields |
| table | `npx shadcn@latest add table` | Team grid (combined with TanStack Table) |
| dropdown-menu | `npx shadcn@latest add dropdown-menu` | Column visibility toggle, conference/division filter |
| badge | `npx shadcn@latest add badge` | ATS result labels (Cover/Loss/Push) in game log |
| skeleton | `npx shadcn@latest add skeleton` | Loading states for grid and panels |
| scroll-area | `npx shadcn@latest add scroll-area` | Horizontal panel strip below grid |
| card | `npx shadcn@latest add card` | Team detail panels |
| separator | `npx shadcn@latest add separator` | Panel dividers |
| label | `npx shadcn@latest add label` | Login form labels |
| alert | `npx shadcn@latest add alert` | Error states |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| TanStack Table | AG Grid Community | AG Grid has more built-in UI but heavier, conflicts with headless+shadcn approach |
| Axios | native fetch | Fetch lacks built-in interceptors; JWT refresh requires more boilerplate with fetch |
| Zustand | React Context | Context causes all consumers to re-render on any state change; Zustand is selective |
| React Router v7 | TanStack Router | TanStack Router is more type-safe but steeper learning curve; project only has 2 routes |
| date-fns | Luxon | date-fns is tree-shakeable and smaller; Luxon is better for timezone-heavy apps |

**Installation (complete set):**
```bash
# Tailwind v4 + shadcn/ui setup
npm install tailwindcss @tailwindcss/vite
npm install -D @types/node

# Data fetching + table
npm install @tanstack/react-query @tanstack/react-table

# Routing + HTTP
npm install react-router-dom axios

# State + dates
npm install zustand date-fns

# Initialize shadcn/ui (interactive — choose 'Default' style, 'Slate' base color, CSS variables: yes)
npx shadcn@latest init

# Add components
npx shadcn@latest add button input table dropdown-menu badge skeleton scroll-area card separator label alert
```

---

## Architecture Patterns

### Recommended Project Structure
```
frontend/src/
├── api/                    # Axios instance, API functions, query hooks
│   ├── client.ts           # Axios instance with interceptors
│   ├── auth.ts             # login(), refresh(), logout() functions
│   └── teams.ts            # getTeams(), getTeamStats(), getTeamGames() + useQuery hooks
├── components/
│   ├── ui/                 # shadcn/ui components (auto-generated, do not edit)
│   ├── auth/
│   │   └── LoginPage.tsx
│   ├── grid/
│   │   ├── TeamGrid.tsx    # TanStack Table integration
│   │   ├── columns.tsx     # ColumnDef[] definitions
│   │   ├── GridToolbar.tsx # Conference/division filter + column visibility toggle
│   │   └── AtsCell.tsx     # Color-coded ATS% cell
│   └── panels/
│       ├── PanelStrip.tsx  # Horizontal scroll container
│       ├── TeamPanel.tsx   # Single team detail panel
│       ├── PanelStats.tsx  # Summary stats section
│       └── GameLog.tsx     # Game-by-game table
├── hooks/
│   └── useLocalStorage.ts  # Generic localStorage persistence hook
├── lib/
│   ├── utils.ts            # shadcn/ui cn() utility (auto-generated)
│   ├── color.ts            # ATS%/O/U% → color gradient calculator
│   └── streak.ts           # Compute streak string from game log array
├── store/
│   └── useAppStore.ts      # Zustand store: openPanels[], auth, theme
├── types/
│   └── api.ts              # TypeScript types for API responses
├── App.tsx                 # Route switch: Login | Main
└── main.tsx                # QueryClient + QueryClientProvider + RouterProvider
```

### Pattern 1: TanStack Query Setup
**What:** QueryClient wraps the application; all data fetching goes through `useQuery` hooks with keys.
**When to use:** Every API call — keeps loading/error/stale states automatic.

```typescript
// Source: https://tanstack.com/query/v5/docs/framework/react/overview
// main.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 60 * 1000,        // 1 minute — data is fresh, grid doesn't refetch on focus
      retry: 1,
    },
  },
})

// Wrap <App /> with <QueryClientProvider client={queryClient}>
```

```typescript
// api/teams.ts
import { useQuery } from '@tanstack/react-query'
import { apiClient } from './client'

export function useTeams() {
  return useQuery({
    queryKey: ['teams'],
    queryFn: () => apiClient.get<TeamStatsResponse[]>('/api/teams').then(r => r.data),
  })
}

export function useTeamStats(teamId: number) {
  return useQuery({
    queryKey: ['team-stats', teamId],
    queryFn: () => apiClient.get<TeamDetailResponse>(`/api/teams/${teamId}/stats`).then(r => r.data),
    enabled: !!teamId,
  })
}

export function useTeamGames(teamId: number) {
  return useQuery({
    queryKey: ['team-games', teamId],
    queryFn: () => apiClient.get<GameLogEntry[]>(`/api/teams/${teamId}/games`).then(r => r.data),
    enabled: !!teamId,
  })
}
```

### Pattern 2: Axios Interceptor for JWT Refresh
**What:** Axios response interceptor catches 401, calls refresh endpoint, updates stored tokens, retries original request once.
**When to use:** All authenticated API calls — transparent to calling code.

```typescript
// api/client.ts
import axios from 'axios'

// Access token stored in memory (not localStorage — XSS mitigation)
// Refresh token stored in localStorage (needed to survive page reload)
let accessToken: string | null = null

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '',
})

export function setAccessToken(token: string | null) {
  accessToken = token
}

// Request interceptor: attach access token to every request
apiClient.interceptors.request.use(config => {
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`
  }
  return config
})

// Response interceptor: on 401, try refresh once then redirect to login
let isRefreshing = false
let failedQueue: Array<{ resolve: (v: unknown) => void; reject: (e: unknown) => void }> = []

apiClient.interceptors.response.use(
  response => response,
  async error => {
    const original = error.config
    if (error.response?.status === 401 && !original._retry) {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject })
        }).then(() => apiClient(original))
      }

      original._retry = true
      isRefreshing = true

      try {
        const refreshToken = localStorage.getItem('refreshToken')
        if (!refreshToken) throw new Error('No refresh token')

        const { data } = await axios.post('/api/auth/refresh', { refreshToken })
        setAccessToken(data.accessToken)
        localStorage.setItem('refreshToken', data.refreshToken)

        failedQueue.forEach(p => p.resolve(undefined))
        failedQueue = []

        return apiClient(original)
      } catch (refreshError) {
        failedQueue.forEach(p => p.reject(refreshError))
        failedQueue = []
        setAccessToken(null)
        localStorage.removeItem('refreshToken')
        window.location.href = '/login'
        return Promise.reject(refreshError)
      } finally {
        isRefreshing = false
      }
    }
    return Promise.reject(error)
  }
)
```

### Pattern 3: TanStack Table with Column Visibility Persisted to localStorage
**What:** Column visibility state initialized from localStorage; changes written back via `onColumnVisibilityChange`.
**When to use:** GRID-04 requirement.

```typescript
// components/grid/TeamGrid.tsx
import { useReactTable, getCoreRowModel, getSortedRowModel,
         getFilteredRowModel, VisibilityState, SortingState } from '@tanstack/react-table'
import { columns } from './columns'
import { useLocalStorage } from '@/hooks/useLocalStorage'

const DEFAULT_VISIBILITY: VisibilityState = {
  team: true,
  record: true,
  streak: true,
  atsPct: true,
  atsRecord: true,
  ouPct: true,
  ouRecord: true,
  // hidden by default:
  conference: false,
  division: false,
}

export function TeamGrid() {
  const { data: teams = [], isLoading } = useTeams()
  const [columnVisibility, setColumnVisibility] = useLocalStorage<VisibilityState>(
    'nba-grid-column-visibility',
    DEFAULT_VISIBILITY
  )
  const [sorting, setSorting] = React.useState<SortingState>([
    { id: 'atsPct', desc: true }   // GRID-01 default: ATS% descending
  ])
  const [columnFilters, setColumnFilters] = React.useState([])

  const table = useReactTable({
    data: teams,
    columns,
    state: { sorting, columnFilters, columnVisibility },
    onSortingChange: setSorting,
    onColumnFiltersChange: setColumnFilters,
    onColumnVisibilityChange: setColumnVisibility,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
  })
  // ... render rows with onClick -> open panel
}
```

### Pattern 4: ATS% Color Gradient Function (Claude's Discretion)
**What:** Maps a percentage to a green/red background color with intensity scaled to distance from 50%.
**Recommendation:** Inline style with HSL color — avoids Tailwind JIT class-name generation issues.

```typescript
// lib/color.ts
/**
 * Returns a CSS background-color string for a percentage value.
 * Above 50% → green (HSL 142), below 50% → red (HSL 0).
 * Distance from 50% controls lightness (further = more saturated).
 * Pushes are excluded from the percentage calculation before calling this.
 */
export function getPercentageColor(pct: number): React.CSSProperties {
  const distance = Math.abs(pct - 50)         // 0–50 range
  // Map distance: 0 = neutral (no color), 10 = light, 25+ = strong
  if (distance < 2) return {}                  // too close to call — no color

  const saturation = Math.min(distance * 3, 80)  // 6%–80% saturation
  const lightness = 95 - distance * 0.8          // 95%–55% lightness
  const hue = pct >= 50 ? 142 : 0               // green or red hue

  return {
    backgroundColor: `hsl(${hue}, ${saturation}%, ${lightness}%)`,
    // Dark mode: use data-theme aware colors or CSS variables instead
  }
}
```

**Dark mode consideration:** HSL at high lightness will wash out in dark mode. Use CSS custom properties:

```css
/* In globals.css — dark mode color anchors */
:root {
  --color-positive: 142 76% 36%;  /* green */
  --color-negative: 0 84% 60%;    /* red */
}
.dark {
  --color-positive: 142 55% 45%;
  --color-negative: 0 70% 55%;
}
```

### Pattern 5: Dark Mode Toggle (Tailwind class strategy)
**What:** Toggle `dark` class on `<html>` element; persist to localStorage.
**When to use:** User theme control (locked decision).

```typescript
// store/useAppStore.ts (Zustand slice)
import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface AppStore {
  theme: 'light' | 'dark'
  toggleTheme: () => void
  openPanels: number[]
  openPanel: (teamId: number) => void
  closePanel: (teamId: number) => void
}

export const useAppStore = create<AppStore>()(
  persist(
    (set) => ({
      theme: 'light',
      toggleTheme: () => set(state => {
        const next = state.theme === 'light' ? 'dark' : 'light'
        document.documentElement.classList.toggle('dark', next === 'dark')
        return { theme: next }
      }),
      openPanels: [],
      openPanel: (teamId) => set(state => {
        if (state.openPanels.includes(teamId)) {
          // Scroll to existing panel instead of adding (per user decision)
          document.getElementById(`panel-${teamId}`)?.scrollIntoView({ behavior: 'smooth', inline: 'nearest' })
          return state
        }
        return { openPanels: [...state.openPanels, teamId] }
      }),
      closePanel: (teamId) => set(state => ({
        openPanels: state.openPanels.filter(id => id !== teamId),
      })),
    }),
    { name: 'nba-app-store', partialize: state => ({ theme: state.theme }) }
  )
)
```

### Pattern 6: Horizontal Panel Strip
**What:** ScrollArea with `orientation="horizontal"` containing a flex row of fixed-width panels.
**When to use:** PANEL-01 through PANEL-05.

```typescript
// components/panels/PanelStrip.tsx
import { ScrollArea, ScrollBar } from '@/components/ui/scroll-area'

export function PanelStrip() {
  const openPanels = useAppStore(s => s.openPanels)

  if (openPanels.length === 0) return null

  return (
    <div className="mt-4 border-t pt-4">
      <ScrollArea className="w-full whitespace-nowrap">
        <div className="flex gap-4 pb-4">
          {openPanels.map(teamId => (
            <TeamPanel key={teamId} teamId={teamId} />
          ))}
        </div>
        <ScrollBar orientation="horizontal" />
      </ScrollArea>
    </div>
  )
}
```

### Pattern 7: Nginx API Proxy for Production
**What:** Nginx in the frontend container proxies `/api/*` to the API container. This eliminates CORS issues in production.
**When to use:** docker-compose production deployment.

```nginx
# frontend/nginx.conf — updated to add API proxy
server {
    listen 80;
    server_name localhost;
    root /usr/share/nginx/html;
    index index.html;

    # Proxy API calls to backend service
    location /api/ {
        proxy_pass http://api:8080/api/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }

    # SPA fallback — all other routes serve index.html
    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

In development, Vite's built-in proxy serves the same purpose:

```typescript
// vite.config.ts — add proxy for dev server
server: {
  proxy: {
    '/api': {
      target: 'http://localhost:5000',
      changeOrigin: true,
    }
  }
}
```

### Pattern 8: Streak Computation from Game Log
**What:** Given a sorted-desc game log, walk from the most recent game and count consecutive wins or losses.

```typescript
// lib/streak.ts
export type StreakResult = { type: 'W' | 'L'; count: number } | null

export function computeStreak(games: GameLogEntry[], teamId: number): StreakResult {
  if (games.length === 0) return null

  // games are sorted descending by date (most recent first)
  const first = games[0]
  const won = (first.isHomeGame && (first.homeScore ?? 0) > (first.awayScore ?? 0))
           || (!first.isHomeGame && (first.awayScore ?? 0) > (first.homeScore ?? 0))
  const streakType: 'W' | 'L' = won ? 'W' : 'L'

  let count = 0
  for (const game of games) {
    const gameWon = (game.isHomeGame && (game.homeScore ?? 0) > (game.awayScore ?? 0))
                 || (!game.isHomeGame && (game.awayScore ?? 0) > (game.homeScore ?? 0))
    if ((gameWon && streakType === 'W') || (!gameWon && streakType === 'L')) {
      count++
    } else {
      break
    }
  }

  return { type: streakType, count }
}
```

### Anti-Patterns to Avoid
- **Storing the access token in localStorage:** XSS vulnerability. Keep access token in module-level memory (JS closure). Only refresh token in localStorage (survives page reload).
- **Filtering/sorting on raw API data in component render:** All filtering/sorting state goes through TanStack Table's state model. Never filter data with `.filter()` before passing to TanStack Table — it breaks TanStack Table's row model.
- **Generating Tailwind color classes dynamically:** Tailwind v4 purges unused classes at build. Dynamic classes like `bg-green-${intensity}` will not work. Use inline styles or CSS custom properties for the color gradient.
- **Fetching all team game logs on page load:** 30 concurrent requests for game logs is expensive. Only fetch a team's game log when its panel is opened (TanStack Query `enabled: openPanels.includes(teamId)`).
- **Using React Context for openPanels state:** Every grid row re-renders when context changes. Use Zustand's selective subscription: `useAppStore(s => s.openPanels)`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Table sorting, filtering, column visibility | Custom sort/filter logic | TanStack Table | Edge cases: multi-column sort, filter composition, VisibilityState type safety |
| JWT refresh race condition | Manual retry flag | Axios interceptor with queue pattern | Race condition: multiple 401s while refreshing must all wait and retry, not trigger multiple refresh calls |
| Column visibility localStorage sync | Custom useEffect | `useLocalStorage` hook | SSR safety, parse error handling, stale closure bugs |
| Accessible dropdown for column visibility | Custom div-based menu | shadcn/ui DropdownMenu | Keyboard nav, focus trap, ARIA attributes are non-trivial |
| Accessible input components | Custom inputs | shadcn/ui Input, Label | Radix UI primitives handle all a11y requirements |
| Horizontal scrollable container | overflow-x: auto on a div | shadcn/ui ScrollArea | Custom scrollbars, consistent cross-browser behavior, keyboard scroll |
| Loading states | div spinners | shadcn/ui Skeleton | Consistent design language, layout stability |
| Date formatting | manual string ops | date-fns `formatDistanceToNow`, `format` | DST, locale, edge cases |

**Key insight:** The shadcn/ui + TanStack Table combination is the current standard for data-heavy React apps. The headless table handles all state logic; shadcn/ui primitives handle all accessible presentation. Don't split these — they are designed to work together.

---

## Common Pitfalls

### Pitfall 1: Access Token Expiry During Grid Load
**What goes wrong:** The team grid's query fires on page load. If the user's access token has already expired (page was idle for 15+ minutes), GET /api/teams returns 401. The interceptor must refresh the token before the user sees any data.
**Why it happens:** TanStack Query fires on mount; access token is not refreshed proactively.
**How to avoid:** The Axios interceptor pattern (Pattern 2) handles this transparently. On app startup, initialize by attempting a token refresh if a refreshToken exists in localStorage.
**Warning signs:** Grid shows empty or "Unauthorized" on fresh load after idle period.

### Pitfall 2: Dynamic Tailwind Classes Not Generated
**What goes wrong:** `bg-green-${shade}` or `text-red-${intensity}` classes are purged at build time because Tailwind cannot detect dynamic class names.
**Why it happens:** Tailwind v4 (and v3) statically analyzes source files for class names. String concatenation produces class names that are not in the static analysis.
**How to avoid:** Use inline `style` attribute for the gradient color. Or use a static map: `const colorMap = { 50: 'bg-green-50', 100: 'bg-green-100', ... }` but this gets unwieldy.
**Warning signs:** Colors work in dev but disappear in production build.

### Pitfall 3: TanStack Table and Custom Cell Renders Ignoring Dark Mode
**What goes wrong:** Custom cell renderers use hardcoded colors (e.g. `#22c55e` green) that look wrong in dark mode.
**Why it happens:** Inline styles bypass Tailwind's dark: variant.
**How to avoid:** Use CSS custom properties for theme-aware colors (see Pattern 4). The `dark` class on `<html>` switches the CSS variables. Inline styles that reference `var(--color-positive)` adapt automatically.
**Warning signs:** Color-coded cells look washed out or invisible in dark mode.

### Pitfall 4: Multiple Panels Opening Duplicate Requests
**What goes wrong:** Clicking the same team row multiple times opens duplicate panels with independent TanStack Query fetches.
**Why it happens:** `openPanels` array has duplicate teamIds.
**How to avoid:** The Zustand `openPanel` action checks `includes(teamId)` before adding. TanStack Query deduplicates by query key so even if two panels share a teamId, only one network request fires. But the UI should not show two panels for the same team.
**Warning signs:** More than 30 panels can be opened; game log table appears twice for same team.

### Pitfall 5: Streak Column Missing from Grid API Response
**What goes wrong:** TEAM-02 requires streak in the grid column, but `GET /api/teams` does not return a streak field.
**Why it happens:** `TeamStatsResponse` was defined in Phase 3 without streak. Computing streak requires iterating the game log (ordered by date) — that data exists in the `finalGames` list already loaded in `GetAllTeamsAsync`.
**How to avoid:** Add `Streak` (int: positive = W streak, negative = L streak, 0 = undefined) to `TeamStatsResponse` and compute it in the backend using the already-loaded `finalGames` partition. This is one extra O(n) pass per team over already-materialized data.
**Warning signs:** Streak column shows blank for all teams; 30 extra HTTP requests fire on grid load.

### Pitfall 6: shadcn/ui init and Tailwind v4 Compatibility
**What goes wrong:** Running `npx shadcn@latest init` with an incompatible Tailwind version produces errors or unstyled components.
**Why it happens:** shadcn/ui's `init` command detects the Tailwind version and generates either a v3 `tailwind.config.ts` or v4 CSS-first config. If the project has Tailwind v4 installed but the old config format, components do not render.
**How to avoid:** Follow the official Vite installation guide at ui.shadcn.com/docs/installation/vite exactly. The guide uses Tailwind v4 with `@tailwindcss/vite` plugin. Run `npx shadcn@latest init` AFTER Tailwind v4 is installed and configured.
**Warning signs:** shadcn/ui components render without any styles; `@layer` directives cause PostCSS errors.

### Pitfall 7: Vite Proxy Not Applied in Docker Production
**What goes wrong:** In development, `vite.config.ts` proxy routes `/api` to `localhost:5000`. In production Docker, the Vite dev server does not exist — nginx must proxy.
**Why it happens:** Developers rely on Vite proxy and forget nginx configuration.
**How to avoid:** Add `location /api/` block to `nginx.conf` (Pattern 7). Test with `docker compose build && docker compose up` before considering the task complete.
**Warning signs:** API calls return nginx 404 in Docker; all calls work in `npm run dev` but fail in container.

---

## Code Examples

Verified patterns from official sources and the existing project:

### TypeScript Types Matching the Phase 3 API

```typescript
// src/types/api.ts
// Source: inferred from nba-lines-tracker/src/NbaTracker.Api/Models/TeamModels.cs (Phase 3)

export interface TeamStatsResponse {
  teamId: number
  name: string
  abbreviation: string
  conference: string | null
  division: string | null
  gamesPlayed: number
  wins: number
  losses: number
  atsCovers: number
  atsLosses: number
  atsPushes: number
  ouOvers: number
  ouUnders: number
  ouPushes: number
  // streak: number  -- NOT YET IN API; must be added or computed (see Open Questions)
}

export interface HomeAwaySplit {
  gamesPlayed: number
  wins: number
  losses: number
  atsCovers: number
  atsLosses: number
  atsPushes: number
  ouOvers: number
  ouUnders: number
  ouPushes: number
}

export interface TeamDetailResponse {
  teamId: number
  name: string
  abbreviation: string
  conference: string | null
  division: string | null
  home: HomeAwaySplit
  away: HomeAwaySplit
}

export interface GameLogEntry {
  gameId: number
  gameDate: string          // DateOnly serialized as "YYYY-MM-DD"
  homeTeamAbbr: string
  awayTeamAbbr: string
  homeScore: number | null
  awayScore: number | null
  isHomeGame: boolean
  spreadLine: number | null
  totalLine: number | null
  atsResult: 'Cover' | 'Loss' | 'Push' | null
  ouResult: 'Over' | 'Under' | 'Push' | null
}

// Derived calculations (NOT from API — computed in frontend)
export function calcAtsPct(stats: { atsCovers: number; atsLosses: number }): number | null {
  const total = stats.atsCovers + stats.atsLosses  // pushes excluded
  if (total === 0) return null
  return (stats.atsCovers / total) * 100
}

export function calcOuPct(stats: { ouOvers: number; ouUnders: number }): number | null {
  const total = stats.ouOvers + stats.ouUnders  // pushes excluded
  if (total === 0) return null
  return (stats.ouOvers / total) * 100
}
```

### TanStack Table Column Definitions

```typescript
// src/components/grid/columns.tsx
// Source: https://ui.shadcn.com/docs/components/data-table and TanStack Table v8 docs

import { ColumnDef } from '@tanstack/react-table'
import { TeamStatsResponse, calcAtsPct, calcOuPct } from '@/types/api'
import { getPercentageColor } from '@/lib/color'
import { ArrowUpDown } from 'lucide-react'

export const teamColumns: ColumnDef<TeamStatsResponse>[] = [
  {
    id: 'team',
    accessorKey: 'name',
    header: ({ column }) => (
      <button onClick={() => column.toggleSorting()} className="flex items-center gap-1">
        Team <ArrowUpDown className="h-4 w-4" />
      </button>
    ),
    enableHiding: false,   // team name always visible
  },
  {
    id: 'record',
    header: 'W-L',
    accessorFn: row => row.wins,
    cell: ({ row }) => `${row.original.wins}-${row.original.losses}`,
    sortingFn: (a, b) => a.original.wins - b.original.wins,
  },
  {
    id: 'atsPct',
    header: ({ column }) => (
      <button onClick={() => column.toggleSorting()} className="flex items-center gap-1">
        ATS% <ArrowUpDown className="h-4 w-4" />
      </button>
    ),
    accessorFn: row => calcAtsPct(row),
    cell: ({ getValue }) => {
      const pct = getValue<number | null>()
      if (pct === null) return '–'
      return (
        <span style={getPercentageColor(pct)} className="px-2 py-1 rounded text-sm font-medium tabular-nums">
          {pct.toFixed(1)}%
        </span>
      )
    },
    sortingFn: (a, b) => {
      const aVal = calcAtsPct(a.original) ?? -1
      const bVal = calcAtsPct(b.original) ?? -1
      return aVal - bVal
    },
  },
  // ... similar for ouPct, atsRecord, ouRecord, streak
]
```

### useLocalStorage Hook

```typescript
// src/hooks/useLocalStorage.ts
// Source: pattern from usehooks-ts.com, adapted for VisibilityState
import { useState, useEffect } from 'react'

export function useLocalStorage<T>(key: string, defaultValue: T): [T, (value: T | ((prev: T) => T)) => void] {
  const [value, setValue] = useState<T>(() => {
    try {
      const stored = window.localStorage.getItem(key)
      return stored ? (JSON.parse(stored) as T) : defaultValue
    } catch {
      return defaultValue
    }
  })

  useEffect(() => {
    try {
      window.localStorage.setItem(key, JSON.stringify(value))
    } catch {
      // Ignore write errors (private browsing, quota exceeded)
    }
  }, [key, value])

  return [value, setValue]
}
```

### vite.config.ts Final Configuration

```typescript
// frontend/vite.config.ts
import path from 'path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| react-query (v3) | @tanstack/react-query v5 | 2023 | Single-object options API; `useSuspenseQuery` stable; ~20% smaller |
| react-table v7 | @tanstack/react-table v8 | 2022 | Full TypeScript rewrite; consistent API across frameworks |
| Tailwind v3 with tailwind.config.ts | Tailwind v4 with CSS-first config | 2025 | No tailwind.config.ts file; config lives in CSS; shadcn/ui now defaults to v4 |
| Redux / Context for global state | Zustand | 2022-present | No boilerplate, no Provider, selective subscription |
| React Router v5/v6 | React Router v7 (library mode) | 2024 | `createBrowserRouter` API; v7 is stable; backward compatible with v6 patterns |
| Manual localStorage sync | Zustand `persist` middleware | 2022-present | Automatic serialization; handles hydration safely |

**Deprecated/outdated:**
- `react-query` npm package: Replaced by `@tanstack/react-query`. The old package is abandoned.
- `react-table` npm package (v7): Replaced by `@tanstack/react-table`. Do not install `react-table`.
- `tailwind.config.ts` with `darkMode: 'class'`: Tailwind v4 does not use this config file format. Dark mode in v4 uses `@variant dark (&:is(.dark *))` in CSS.
- shadcn/ui v2.3.0 or older: Only compatible with Tailwind v3. Use `npx shadcn@latest` (no version pin) for Tailwind v4.

---

## Open Questions

1. **Streak field missing from GET /api/teams response (CRITICAL — blocks TEAM-02 grid column)**
   - What we know: `TeamStatsResponse` from Phase 3 has wins/losses but no streak. The API's `GetAllTeamsAsync` already materializes all `finalGames` partitioned per team, so computing streak is O(n) per team over already-loaded data.
   - What's unclear: Should streak be added to the API (backend change), or should the frontend compute streak from GET /api/teams/{id}/games (30 extra requests on page load)?
   - Recommendation: **Add streak to the API**. In `TeamEndpoints.GetAllTeamsAsync`, after partitioning `homeGames` and `awayGames`, sort all team games by date descending and walk the array to count consecutive wins or losses. Return as `Streak` (int, positive = W streak, negative = L streak). This is a minor addition to Phase 3's `TeamStatsResponse` record. The alternative (30 extra game-log requests on frontend page load) is a poor UX and adds 30 API calls.

2. **GRID-06 "Last synced" data source**
   - What we know: GET /api/admin/sync-status returns the last 10 SyncRun records for admin users. Regular users cannot call this endpoint (403 for non-admin).
   - What's unclear: Should GET /api/teams return a `lastSynced` timestamp? Or should the frontend call sync-status (admin only, so not available to all users)? Or should we add a public /api/status endpoint?
   - Recommendation: **Add a `lastSyncedAt` field to the GET /api/teams response** (or as a response header). This is one database lookup (latest CompletedAt from SyncRuns) and makes the data available to all authenticated users without privilege escalation.

3. **Access token initialization on page reload**
   - What we know: Access tokens are stored in memory (not localStorage). On page reload, memory is cleared. The refresh token is in localStorage.
   - What's unclear: The app needs to call POST /api/auth/refresh on startup to restore the access token before the first authenticated API call fires.
   - Recommendation: In `App.tsx`, run a `useEffect` on mount that reads `localStorage.getItem('refreshToken')` and calls the refresh endpoint. Show a loading state until this check completes. If refresh fails (expired/revoked), redirect to login.

---

## Sources

### Primary (HIGH confidence)
- Official shadcn/ui Vite installation docs (https://ui.shadcn.com/docs/installation/vite) — Tailwind v4 setup, shadcn init command, tsconfig paths
- Phase 3 source code (`nba-lines-tracker/src/NbaTracker.Api/Models/TeamModels.cs`) — exact API response shapes
- Phase 3 source code (`nba-lines-tracker/src/NbaTracker.Api/Endpoints/TeamEndpoints.cs`) — endpoint behavior
- Existing frontend (`nba-lines-tracker/frontend/package.json`, `vite.config.ts`, `Dockerfile`) — existing versions and build setup

### Secondary (MEDIUM confidence)
- TanStack Query v5 npm page (https://www.npmjs.com/package/@tanstack/react-query) — version 5.90.21 confirmed
- TanStack Table v8 npm page (https://www.npmjs.com/package/@tanstack/react-table) — version 8.21.3 confirmed
- shadcn/ui Data Table docs (https://ui.shadcn.com/docs/components/data-table) — column visibility and sorting patterns verified
- shadcn/ui ScrollArea docs (https://ui.shadcn.com/docs/components/scroll-area) — horizontal scrollbar pattern verified
- Vite docs server options (https://vite.dev/config/server-options) — proxy configuration verified

### Tertiary (LOW confidence)
- Gradient color implementation using HSL (from multiple community sources, no single authoritative source) — verify dark mode rendering in practice
- Zustand v5 `persist` middleware API — verify against Zustand changelog before use

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — confirmed versions from npm, existing project files verified
- Architecture: HIGH — patterns derived from official shadcn/ui + TanStack docs and existing API shapes
- Pitfalls: MEDIUM — dynamic Tailwind classes and JWT refresh race conditions verified by multiple sources; dark mode color behavior LOW (needs empirical testing)
- Open questions: HIGH — streak gap is verifiable by reading TeamModels.cs

**Research date:** 2026-02-20
**Valid until:** 2026-03-20 (30 days — Tailwind v4 and shadcn/ui are actively evolving; re-verify if > 30 days)
