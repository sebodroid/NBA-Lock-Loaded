# Phase 2: Ingestion Worker - Context

**Gathered:** 2026-02-18
**Status:** Ready for planning

<domain>
## Phase Boundary

A .NET Worker Service that pulls NBA game data from BallDontLie and betting lines from The Odds API, calculates ATS/O/U results, and writes verified data to PostgreSQL. Runs on a daily schedule and includes an initial season load for the 2025-26 season. Scope is current season (2025-26) onwards — no 2024-25 historical backfill.

</domain>

<decisions>
## Implementation Decisions

### Season scope
- Target season: 2025-26 (current), from game 1 through end of season
- Continue into future seasons automatically — no season boundary hardcoded
- No 2024-25 historical backfill required

### Sync schedule
- One combined daily job at 5am ET (scores and lines fetched together)
- Detects gaps on startup: if last successful sync is >1 day old, backfills missed days before resuming normal schedule

### Initial season load (first run)
- Manually triggered via CLI arg or env flag (e.g. `BACKFILL=true` in `.env` or `dotnet run -- --backfill`)
- Loads all 2025-26 games from game 1 (October 2025) to present
- Same code path as daily sync, just with a date range override

### Failure handling
- API failures: retry 3x with backoff, then continue with whatever data was successfully fetched — do NOT abort the whole run
- Every failure (API error, retry exhausted) logged to sync_runs with full error details
- Partial runs continue and complete; missing data is noted, not silently dropped

### Unresolved games
- If betting lines are missing for a game: store the game record with null ATS/O/U fields, mark game as unresolved
- Unresolved games are re-attempted on the next sync run (lines may arrive later)

### Sync run status
- Three-value enum: `SUCCESS` / `PARTIAL` / `FAILURE`
- SUCCESS: all games and lines fetched and processed cleanly
- PARTIAL: some data fetched but errors occurred (e.g. lines missing for some games, one API call failed after retries)
- FAILURE: sync could not complete in any meaningful way (e.g. both APIs unreachable)
- Error details stored in sync_runs for admin visibility

### Claude's Discretion
- Exact retry backoff strategy (exponential vs fixed delay)
- Database transaction scope per sync run
- How gap detection identifies missed days (compare sync_runs timestamps vs expected schedule)
- Rate limiting implementation between API calls

</decisions>

<specifics>
## Specific Ideas

- The admin status endpoint (`/api/admin/sync-status`) surfacing sync_runs data is Phase 3 — Phase 2 just needs to write the right data to sync_runs
- Canonical sportsbook selection (FanDuel primary, HardRock fallback) and matching key (`{season}_{home_abbr}_{away_abbr}_{game_date_utc}`) are already locked from Phase 1 decisions

</specifics>

<deferred>
## Deferred Ideas

- None — discussion stayed within phase scope

</deferred>

---

*Phase: 02-ingestion-worker*
*Context gathered: 2026-02-18*
