---
phase: 04-react-frontend
plan: 02
subsystem: ui
tags: [react, tanstack-table, tailwind, shadcn, zustand, typescript]

# Dependency graph
requires:
  - phase: 04-01
    provides: "App foundation — Tailwind v4, shadcn/ui components, Zustand store, useLocalStorage hook, color.ts utility, types/api.ts"
provides:
  - "TeamGrid component with TanStack Table v8, sort, filter, column visibility, localStorage persistence"
  - "AtsCell color-coded percentage display using getPercentageColor"
  - "GridToolbar with conference toggle buttons and division dropdown"
  - "columns.tsx with 7 visible + 2 hidden ColumnDef[] for TeamStatsResponse"
affects: [04-03, 04-04]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Pre-filter data array before passing to TanStack Table (not TanStack column filters) for conference/division"
    - "accessorFn for derived numeric values (atsPct, ouPct) enables correct numeric sort"
    - "Custom sortingFn maps null to -1 so null values sort last"
    - "useLocalStorage hook wires column visibility to localStorage key 'nba-grid-column-visibility'"
    - "DEFAULT_SORT = [{ id: 'atsPct', desc: true }] — best cover teams at top by default"

key-files:
  created:
    - nba-lines-tracker/frontend/src/components/grid/AtsCell.tsx
    - nba-lines-tracker/frontend/src/components/grid/columns.tsx
    - nba-lines-tracker/frontend/src/components/grid/GridToolbar.tsx
    - nba-lines-tracker/frontend/src/components/grid/TeamGrid.tsx
  modified: []

key-decisions:
  - "Pre-filter data before TanStack Table rather than using TanStack column filters — avoids dual-filter row model issues"
  - "teamColumns has 9 entries: 7 visible (team, record, streak, atsPct, atsRecord, ouPct, ouRecord) + 2 hidden (conference, division)"
  - "team column has enableHiding: false — team name always visible, not toggleable"
  - "AtsCell reused for both ATS% and O/U% columns — same color logic applies"

patterns-established:
  - "SortHeader: reusable header component with ArrowUpDown icon + toggleSorting() for all sortable columns"
  - "Column id naming: camelCase (atsPct, ouPct, atsRecord, ouRecord) matching DEFAULT_VISIBILITY keys"
  - "Error state: rounded-md border border-destructive/50 bg-destructive/10 pattern for API failures"

requirements-completed: [TEAM-01, TEAM-02, TEAM-03, TEAM-04, GRID-01, GRID-02, GRID-03, GRID-04, GRID-05]

# Metrics
duration: 12min
completed: 2026-02-20
---

# Phase 4 Plan 02: Team Grid Summary

**TanStack Table v8 team grid with color-coded ATS%/O/U% cells, conference/division filters, toggleable column visibility persisted to localStorage, and default ATS% descending sort**

## Performance

- **Duration:** 12 min
- **Started:** 2026-02-20T22:26:28Z
- **Completed:** 2026-02-20T22:38:00Z
- **Tasks:** 2
- **Files modified:** 4 created

## Accomplishments
- TeamGrid renders all 30 NBA teams with 7 default visible columns and 2 toggleable hidden columns (conference, division)
- AtsCell component applies green/red backgrounds via getPercentageColor, scaled by distance from 50%
- GridToolbar provides East/West conference toggle buttons, 6-division dropdown, and column visibility dropdown
- Column visibility persisted to localStorage under key 'nba-grid-column-visibility' via useLocalStorage hook
- Row click calls openPanel(teamId) via Zustand store to open team panel
- Default sort is ATS% descending — best cover teams surface at top

## Task Commits

Each task was committed atomically:

1. **Task 1: Column definitions and color-coded AtsCell** - `8db4618` (feat)
2. **Task 2: GridToolbar and TeamGrid with full TanStack Table integration** - `9a0fdf5` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `nba-lines-tracker/frontend/src/components/grid/AtsCell.tsx` - Color-coded percentage cell, renders dash for null
- `nba-lines-tracker/frontend/src/components/grid/columns.tsx` - 9 ColumnDef[] entries (7 visible + 2 hidden), SortHeader component
- `nba-lines-tracker/frontend/src/components/grid/GridToolbar.tsx` - Conference toggle, division dropdown, column visibility dropdown
- `nba-lines-tracker/frontend/src/components/grid/TeamGrid.tsx` - Full TanStack Table integration with sort, pre-filter, visibility, skeleton, error state

## Decisions Made
- Pre-filtering the data array (not TanStack column filters) for conference/division — cleaner approach without dual-filter issues since we're not using TanStack's `getFilteredRowModel()`
- `teamColumns` uses `id` strings matching `DEFAULT_VISIBILITY` keys exactly so TanStack Table state reconciles correctly
- `team` column has `enableHiding: false` per requirement (team name always visible)
- `AtsCell` reused for both ATS% and O/U% — identical color semantics

## Deviations from Plan

None — plan executed exactly as written. The plan note about removing `getFilteredRowModel` was followed (it's absent from the TeamGrid table config).

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- TeamGrid is complete and ready to be imported into MainPage (04-03)
- All grid sub-components are independently testable
- openPanel wiring is in place for team panel display (04-03)

---
*Phase: 04-react-frontend*
*Completed: 2026-02-20*
