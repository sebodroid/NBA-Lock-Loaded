# NBA Lines Tracker

## What This Is

A full-stack web application for tracking how all 30 NBA teams perform against betting lines — covering the spread (ATS) and hitting the game total (O/U) — for the current season. Small groups of friends can log in, browse team betting performance alongside records and streaks, and compare multiple teams side by side in a dynamic multi-panel layout.

## Core Value

At a glance, see which NBA teams cover the spread and hit the over/under most reliably — presented clearly enough to inform betting decisions in seconds.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Display all 30 NBA teams with their current season record and W/L game streak
- [ ] Show ATS (against-the-spread) stats per team: games covered, games missed, cover %
- [ ] Show O/U (over/under) stats per team: overs hit, unders hit, push count, hit %
- [ ] Main page shows team cards/grid with sortable, filterable columns
- [ ] Clicking a team opens a detail panel — multiple panels can be open simultaneously for side-by-side comparison
- [ ] Columns in the grid are customizable (add/remove fields per user preference)
- [ ] Per-user authentication (individual username/password login)
- [ ] Data refreshes automatically on a daily batch schedule
- [ ] Full Docker containerization for portability
- [ ] PostgreSQL database hosted on Aiven

### Out of Scope

- Player prop tracking (points, assists, rebounds lines) — deferred to v2
- Live/real-time in-game data — daily batch is sufficient for v1
- Public access — invite-only with per-user auth
- Mobile native app — web-first

## Context

- NBA game/schedule data source: RapidAPI "NBA API Free Data" (user-identified). Needs validation that it covers 2024-25 season scores and schedules.
- Betting lines data (spread and totals): No source identified yet — The Odds API (theoddapi.com) is a strong candidate. Has historical odds, spreads, and totals for NBA with a free tier (500 req/month). Alternative: SportsDataIO or ApiSports NBA.
- Data coverage: Current 2024-25 season only for v1.
- The term "prop bets" in the user's description refers to team-level spread and total bets, not individual player props.

## Constraints

- **Tech Stack**: .NET backend, React frontend, PostgreSQL (Aiven), Docker — no deviations
- **Hosting**: Database on Aiven; app containerized for flexible deployment elsewhere
- **Data**: Must integrate with external NBA + odds APIs (no manual data entry)
- **Season Scope**: Current season (2024-25) only for v1 — no multi-season historical depth

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Separate NBA data API + odds API | Game data and betting lines are typically separate data products | — Pending |
| The Odds API for spread/O/U data | Free tier available, well-documented, covers NBA historical odds | — Pending |
| Aiven for PostgreSQL hosting | User requirement — managed cloud Postgres | — Pending |
| Daily batch sync (not real-time) | Simpler architecture, matches user's stated needs | — Pending |
| Multi-panel comparison UI | User explicitly wants side-by-side team comparison without closing panels | — Pending |
| Per-user auth (individual accounts) | Small friend group with individual logins | — Pending |

---
*Last updated: 2026-02-17 after initialization*
