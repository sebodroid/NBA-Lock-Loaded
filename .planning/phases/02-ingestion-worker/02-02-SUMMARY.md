---
phase: 02-ingestion-worker
plan: 02
subsystem: api
tags: [oddsapi, httpclient, resilience, bookmakers, spreads, totals, dotnet]

# Dependency graph
requires:
  - phase: 01-data-foundation
    provides: GameLine entity schema that line extraction maps to
provides:
  - OddsApiEvent DTO with Id, CommenceTime (DateTimeOffset), HomeTeam, AwayTeam, Bookmakers
  - OddsApiBookmaker / OddsApiMarket / OddsApiOutcome DTOs
  - OddsApiScore / OddsApiTeamScore DTOs for completed game scores
  - OddsApiClient typed HttpClient with GetOddsAsync + GetScoresAsync
  - Static SelectCanonicalBookmaker, ExtractSpread, ExtractTotal helpers
  - 3-retry exponential backoff registered in Program.cs
affects: [02-03, 02-04]

# Tech tracking
tech-stack:
  added: []
  patterns: [typed-httpclient, static-helper-methods, config-driven-bookmakers]

key-files:
  created:
    - nba-lines-tracker/src/NbaTracker.Worker/Models/OddsApi/OddsApiEvent.cs
    - nba-lines-tracker/src/NbaTracker.Worker/Models/OddsApi/OddsApiBookmaker.cs
    - nba-lines-tracker/src/NbaTracker.Worker/Models/OddsApi/OddsApiScore.cs
    - nba-lines-tracker/src/NbaTracker.Worker/Services/OddsApiClient.cs
  modified:
    - nba-lines-tracker/src/NbaTracker.Worker/Program.cs

key-decisions:
  - "CommenceTime as DateTimeOffset (not DateTime) — preserves UTC offset for correct ET date conversion"
  - "API key appended as query param (?apiKey=) per The Odds API auth spec — not a header"
  - "Both bookmaker keys configurable (OddsApi:PrimaryBookmaker, OddsApi:FallbackBookmaker) — defaults fanduel/hardrockbet but never hardcoded in logic"
  - "HardRock key 'hardrockbet' LOW confidence — debug log of all available bookmakers on first call for validation"
  - "Full URL not logged to prevent API key exposure in logs"

patterns-established:
  - "Static helper pattern: SelectCanonicalBookmaker/ExtractSpread/ExtractTotal are pure static methods — testable without DI, callable from SyncOrchestrator directly"
  - "Config-driven bookmaker keys: read IConfiguration, never hardcode sportsbook strings in logic"
  - "First-call debug logging: log diagnostic info once, guard with bool flag"

requirements-completed: [DATA-02]

# Metrics
duration: 25min
completed: 2026-02-18
---

# Phase 02-02: The Odds API Client Summary

**OddsApiClient typed HttpClient with configurable canonical bookmaker selection and static ExtractSpread/ExtractTotal helpers for NBA betting line ingestion**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-02-18
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- OddsApiEvent, OddsApiBookmaker/Market/Outcome, OddsApiScore/TeamScore DTOs with correct JsonPropertyName attributes
- OddsApiClient with GetOddsAsync (spreads+totals) and GetScoresAsync (up to 3 days back)
- SelectCanonicalBookmaker: config-driven primary/fallback, no hardcoded keys
- ExtractSpread: favorite by MinBy(Point), returns absolute spread + both sides' odds
- ExtractTotal: Over/Under line extraction
- Registered in Program.cs with AddResilienceHandler (3-retry, exponential backoff, 30s timeout)
- First-call debug log of all available bookmaker keys for HardRock key validation

## Task Commits

1. **Task 1: OddsApi response DTOs** - `d41d9da` (feat, bundled with 02-01 Task 1)
2. **Task 2: OddsApiClient + Program.cs registration** - `62f08a3` (feat)

## Files Created/Modified
- `nba-lines-tracker/src/NbaTracker.Worker/Models/OddsApi/OddsApiEvent.cs` — Event DTO with DateTimeOffset CommenceTime
- `nba-lines-tracker/src/NbaTracker.Worker/Models/OddsApi/OddsApiBookmaker.cs` — Bookmaker/Market/Outcome DTOs
- `nba-lines-tracker/src/NbaTracker.Worker/Models/OddsApi/OddsApiScore.cs` — Score/TeamScore DTOs (score returned as string)
- `nba-lines-tracker/src/NbaTracker.Worker/Services/OddsApiClient.cs` — Typed HttpClient with static helpers
- `nba-lines-tracker/src/NbaTracker.Worker/Program.cs` — OddsApiClient DI registration + AddResilienceHandler

## Decisions Made
- Chose DateTimeOffset for CommenceTime — critical for ET date conversion in 02-03 GameMatchingService
- API key in query param (not header) per The Odds API auth spec
- Logged bookmaker keys at Debug (not Info) to avoid noise in production logs

## Deviations from Plan
None - plan executed exactly as written.

## User Setup Required
- **OddsApi__ApiKey** — The Odds API dashboard → Account → API Key
- **OddsApi__PrimaryBookmaker** — set to `fanduel` (default)
- **OddsApi__FallbackBookmaker** — set to `hardrockbet` (validate key on first run via debug logs)

## Next Phase Readiness
- OddsApiClient ready for SyncOrchestrator (02-04) to call
- Static helpers (SelectCanonicalBookmaker, ExtractSpread, ExtractTotal) ready for 02-04 line matching
- ⚠️ HardRock bookmaker key needs runtime validation — check debug logs on first real run

---
*Phase: 02-ingestion-worker*
*Completed: 2026-02-18*
