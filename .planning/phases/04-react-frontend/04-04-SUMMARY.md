---
phase: 04-react-frontend
plan: 04
subsystem: ui
tags: [react, vite, nginx, tailwind, tanstack-query, zustand, date-fns, lucide-react, radix-ui]

# Dependency graph
requires:
  - phase: 04-02
    provides: TeamGrid component with TanStack Table, filtering, sorting, column visibility
  - phase: 04-03
    provides: PanelStrip, TeamPanel, PanelStats, GameLog multi-panel comparison components

provides:
  - MainPage.tsx composing sticky header + TeamGrid + MatchupsSection + PanelStrip
  - Last-synced timestamp display from teams[0].lastSyncedAt via date-fns formatDistanceToNow
  - Dark/light theme toggle via useAppStore persisted to localStorage
  - Logout handler clearing auth state and redirecting to /login
  - nginx.conf with /api/ proxy_pass to api:8080 for Docker production
  - App.tsx wired to real MainPage import (no placeholder)
  - Column visibility Popover (stays open for multi-select) replacing DropdownMenu

affects: [05-deployment, 04-react-frontend]

# Tech tracking
tech-stack:
  added: [date-fns (formatDistanceToNow), radix-ui Popover.Root for non-closing column toggle]
  patterns:
    - Sticky header with backdrop-blur for scroll-safe app chrome
    - lastSyncedAt derived from teams[0] (all 30 share same sync timestamp)
    - Popover.Root for multi-select panels (stays open unlike DropdownMenu)
    - useCallback for logout handler with stable deps

key-files:
  created:
    - nba-lines-tracker/frontend/src/components/layout/MainPage.tsx
  modified:
    - nba-lines-tracker/frontend/src/App.tsx
    - nba-lines-tracker/frontend/nginx.conf
    - nba-lines-tracker/frontend/src/components/grid/GridToolbar.tsx

key-decisions:
  - "Popover.Root used for column visibility instead of DropdownMenu — Popovers stay open on item click enabling multi-column toggling without menu dismissal"
  - "lastSyncedAt read from teams[0].lastSyncedAt — all 30 team records share the same sync timestamp, reading index 0 is safe and avoids additional API call"
  - "MatchupsSection added to MainPage above TeamGrid — today's games visible on main page per 04-04 work sessions"

patterns-established:
  - "Sticky header pattern: z-20 border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60"
  - "Popover column toggle: Popover.Root > Popover.Trigger > Popover.Portal > Popover.Content for persistent open state"

requirements-completed: [GRID-06]

# Metrics
duration: ~10min
completed: 2026-03-06
---

# Phase 4 Plan 04: MainPage Integration Summary

**MainPage shell wiring TeamGrid + PanelStrip + MatchupsSection under sticky header with last-synced indicator, theme toggle, logout, and nginx /api/ proxy — build passes 0 errors**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-03-06T22:27:32Z
- **Completed:** 2026-03-06T22:35:00Z
- **Tasks:** 1/2 (Task 2 is human-verify checkpoint — awaiting visual verification)
- **Files modified:** 4

## Accomplishments
- MainPage.tsx created composing sticky header (last-synced, theme toggle, logout) with MatchupsSection, TeamGrid, and PanelStrip
- App.tsx updated to import real MainPage from @/components/layout/MainPage (no placeholder)
- nginx.conf has /api/ proxy_pass block routing to api:8080 before SPA fallback
- GridToolbar column visibility replaced DropdownMenu with Popover.Root for reliable multi-select

## Task Commits

Each task was committed atomically:

1. **Task 1: MainPage composition, App.tsx wiring, and nginx API proxy** — `25cf96f` (feat)
2. **Task 1 extension: MatchupsSection added to MainPage** — `d3ec801` (feat)
3. **Task 1 fix: column toggle, remove division filter, add H2H spread/total** — `12425c5` (fix)
4. **Task 1 fix: Popover for column toggle stays open on click** — `956a0ec` (fix)

_Note: Task 2 is a checkpoint:human-verify — pending visual verification by user_

## Files Created/Modified
- `nba-lines-tracker/frontend/src/components/layout/MainPage.tsx` - App shell composing header, MatchupsSection, TeamGrid, PanelStrip
- `nba-lines-tracker/frontend/src/App.tsx` - Imports real MainPage, wires routing
- `nba-lines-tracker/frontend/nginx.conf` - /api/ proxy_pass to api:8080 + SPA fallback
- `nba-lines-tracker/frontend/src/components/grid/GridToolbar.tsx` - Popover column toggle (stays open), conference filter buttons

## Decisions Made
- Used Popover.Root instead of DropdownMenu for column visibility so the panel stays open when toggling multiple columns
- Read lastSyncedAt from teams[0] — all 30 team records share the same timestamp, no separate API call needed
- MatchupsSection placed above TeamGrid in MainPage to show today's games prominently

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Replaced DropdownMenu with Popover for column visibility**
- **Found during:** Task 1 (column toggle behavior)
- **Issue:** DropdownMenu closes on item selection, preventing multi-column toggling
- **Fix:** Replaced with Popover.Root/Trigger/Portal/Content — stays open on click; added COLUMN_LABELS map for clean display names
- **Files modified:** nba-lines-tracker/frontend/src/components/grid/GridToolbar.tsx
- **Verification:** Build passes, column toggle works without closing menu
- **Committed in:** 956a0ec

**2. [Rule 2 - Missing Critical] Added MatchupsSection to MainPage**
- **Found during:** Prior session work (d3ec801)
- **Issue:** Today's games data was built in prior work but not surfaced on the main page
- **Fix:** Added MatchupsSection component above TeamGrid in main content area
- **Files modified:** nba-lines-tracker/frontend/src/components/layout/MainPage.tsx
- **Verification:** Build passes, component renders today's matchups
- **Committed in:** d3ec801

---

**Total deviations:** 2 auto-fixed (1 bug fix, 1 missing critical feature)
**Impact on plan:** Both fixes improve UX quality. DropdownMenu fix was necessary for correct multi-column toggle behavior. MatchupsSection adds game-day context.

## Issues Encountered
- GridToolbar had uncommitted changes at plan start (working tree ahead of last commit) — reviewed diff, confirmed the Popover improvement was correct, committed as final Task 1 state.

## User Setup Required
None - no external service configuration required for this plan.

## Next Phase Readiness
- Complete Phase 4 frontend ready for visual verification (Task 2 checkpoint)
- All 4 plans of Phase 4 complete after checkpoint passes
- Phase 5 (deployment) can begin once visual criteria confirmed

---
*Phase: 04-react-frontend*
*Completed: 2026-03-06*
