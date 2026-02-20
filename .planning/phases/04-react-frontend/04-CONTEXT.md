# Phase 4: React Frontend - Context

**Gathered:** 2026-02-19
**Status:** Ready for planning

<domain>
## Phase Boundary

A web application where authenticated users view NBA team betting stats (ATS/O/U) in a sortable/filterable grid and compare teams side-by-side in multi-panel views. Scope includes: login page, team grid with sort/filter/column visibility, multi-panel team comparison, color coding, and data freshness display. Player props, real-time data, notifications, and multi-season comparisons are out of scope.

</domain>

<decisions>
## Implementation Decisions

### Visual theme & styling
- Both dark mode and light mode — user-controlled toggle
- Tailwind CSS for all styling
- shadcn/ui component library (Radix UI primitives, accessible, copy-paste, no version lock-in)
- Clean / minimal aesthetic — white space, subtle borders, data speaks for itself (think Linear or Notion)

### Grid layout & defaults
- Default visible columns: Team, W-L record, Current streak, ATS%, ATS record (covers/misses/pushes), O/U%, O/U record (overs/unders/pushes)
- Default sort order: ATS% descending — best cover teams appear at top
- Column visibility preference persists to localStorage (survives page refresh and new sessions)
- Clicking anywhere on a team row opens the detail panel — no separate button needed

### Multi-panel behavior
- No hard limit on panels — panels appear below the grid and scroll horizontally as more open
- Panels render in a horizontal strip below the full-width grid
- If a user clicks a team already open in a panel: scroll to and highlight the existing panel (no duplicate panels)
- Close via X button in the panel header only

### Color coding & thresholds
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

</decisions>

<specifics>
## Specific Ideas

- The overall feel should be like Linear or Notion — clean, not cluttered, data-forward
- The team grid ATS% column is the primary reason the app exists — it should be visually prominent and immediately readable
- The panel strip below the grid is inspired by side-by-side comparison patterns (not modal/overlay)

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 04-react-frontend*
*Context gathered: 2026-02-19*
