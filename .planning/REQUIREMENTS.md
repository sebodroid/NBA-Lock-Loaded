# Requirements: NBA Lines Tracker

**Defined:** 2026-02-17
**Core Value:** At a glance, see which NBA teams cover the spread and hit the over/under most reliably

## v1 Requirements

### Authentication

- [ ] **AUTH-01**: User can log in with email and password
- [ ] **AUTH-02**: User session persists across browser refresh (JWT)
- [ ] **AUTH-03**: User can log out from any page
- [ ] **AUTH-04**: Admin can create user accounts (no public self-serve registration)

### Team Overview

- [ ] **TEAM-01**: User can view all 30 NBA teams with current season W-L record
- [ ] **TEAM-02**: User can view current game streak per team (W or L with count)
- [ ] **TEAM-03**: User can view ATS stats per team (covered count, missed count, push count, cover %)
- [ ] **TEAM-04**: User can view O/U stats per team (overs hit, unders hit, push count, hit %)
- [ ] **TEAM-05**: User can view last 10 games ATS and O/U stats per team
- [ ] **TEAM-06**: User can view home split ATS and O/U stats per team
- [ ] **TEAM-07**: User can view away split ATS and O/U stats per team

### Data Grid Controls

- [ ] **GRID-01**: User can sort the team grid by any displayed column
- [ ] **GRID-02**: User can filter teams by conference (East / West)
- [ ] **GRID-03**: User can filter teams by division
- [ ] **GRID-04**: User can show or hide columns in the grid (customizable per user)
- [ ] **GRID-05**: User can see color-coded indicators on ATS% (green above threshold, red below)
- [ ] **GRID-06**: User can see when data was last synced ("Last synced: X ago")

### Multi-Panel Team Comparison

- [ ] **PANEL-01**: User can open a team detail panel by clicking a team in the grid
- [ ] **PANEL-02**: User can have multiple team panels open simultaneously for side-by-side comparison
- [ ] **PANEL-03**: User can open additional team panels without closing existing ones
- [ ] **PANEL-04**: Each panel displays the team's full summary stats (ATS%, O/U%, record, streak, home/away splits)
- [ ] **PANEL-05**: Each panel displays a game-by-game log (opponent, game result, spread line, ATS result, total line, O/U result)

### Data Ingestion

- [ ] **DATA-01**: System syncs NBA game schedules and final scores daily from BallDontLie API
- [ ] **DATA-02**: System syncs betting lines (spread, total) daily from The Odds API (FanDuel as primary book, HardRock as fallback)
- [ ] **DATA-03**: System calculates and stores ATS result (COVER / LOSS / PUSH) for each completed game
- [ ] **DATA-04**: System calculates and stores O/U result (OVER / UNDER / PUSH) for each completed game
- [ ] **DATA-05**: Admin can view sync status (last run time, success/failure, error details)
- [ ] **DATA-06**: System supports one-time historical data load (CSV seed or API backfill) for current 2024-25 season

## v2 Requirements

### Player Props

- **PROP-01**: User can view player prop hit rates (points, assists, rebounds lines)
- **PROP-02**: User can filter player props by team
- **PROP-03**: User can view player prop performance in team detail panel

### Notifications

- **NOTF-01**: User can receive notifications when tracked teams cover or miss the spread
- **NOTF-02**: User can configure notification preferences

### Advanced Analytics

- **ANLX-01**: User can view ATS trend sparklines (last 10 vs season avg)
- **ANLX-02**: User can view ATS performance split by favorite vs. underdog role
- **ANLX-03**: User can save filter presets for quick access to frequently used views

## Out of Scope

| Feature | Reason |
|---------|--------|
| Self-serve user registration | Admin-managed friend group; no public onboarding needed |
| Live / real-time game data | Daily batch produces cleaner, verified data; WebSocket complexity not justified |
| Multi-season historical comparison | Current 2024-25 season only for v1 |
| Social features (comments, picks) | Not the core value; adds moderation complexity |
| Win probability / prediction model | ML infrastructure scope explosion; users draw their own conclusions |
| Mobile native app | Web-first; React SPA works on mobile browsers |
| Multiple sportsbook line comparison | Single canonical book (FanDuel) sufficient for ATS calculation |
| Betting line shopping | Out of scope for v1 |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| AUTH-01 | — | Pending |
| AUTH-02 | — | Pending |
| AUTH-03 | — | Pending |
| AUTH-04 | — | Pending |
| TEAM-01 | — | Pending |
| TEAM-02 | — | Pending |
| TEAM-03 | — | Pending |
| TEAM-04 | — | Pending |
| TEAM-05 | — | Pending |
| TEAM-06 | — | Pending |
| TEAM-07 | — | Pending |
| GRID-01 | — | Pending |
| GRID-02 | — | Pending |
| GRID-03 | — | Pending |
| GRID-04 | — | Pending |
| GRID-05 | — | Pending |
| GRID-06 | — | Pending |
| PANEL-01 | — | Pending |
| PANEL-02 | — | Pending |
| PANEL-03 | — | Pending |
| PANEL-04 | — | Pending |
| PANEL-05 | — | Pending |
| DATA-01 | — | Pending |
| DATA-02 | — | Pending |
| DATA-03 | — | Pending |
| DATA-04 | — | Pending |
| DATA-05 | — | Pending |
| DATA-06 | — | Pending |

**Coverage:**
- v1 requirements: 28 total
- Mapped to phases: 0 (populated during roadmap)
- Unmapped: 28 ⚠️

---
*Requirements defined: 2026-02-17*
*Last updated: 2026-02-17 after initial definition*
