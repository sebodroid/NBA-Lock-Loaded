# PITFALLS.md — NBA Lines Tracker

**Project:** NBA Lines Tracker
**Research Type:** Pitfalls — Greenfield project risk identification
**Date:** 2026-02-17
**Scope:** Sports data ingestion, ATS/O/U betting analytics, .NET + React + PostgreSQL + Docker

---

## How to Read This Document

Each pitfall follows this structure:

- **What goes wrong** — the actual failure mode
- **Warning signs** — how to detect it early (before it bites you)
- **Prevention strategy** — specific, actionable steps
- **Phase** — which development phase should address this

Phases: `[Schema]` `[Ingestion]` `[Sync]` `[Calc]` `[API]` `[Auth]` `[Infra]` `[Testing]`

---

## Part 1: Sports Data API Pitfalls

---

### PITFALL-01: Treating API IDs as Stable Across Providers

**What goes wrong:**
The game ID from your NBA data API (e.g., BallDontLie, SportsData.io, RapidAPI NBA) is completely different from the game ID in your odds API (e.g., The Odds API, OddsJam). There is no universal game ID standard in sports data. You will be storing odds for game `theoddsapi:NBA_20260117_BOS_MIA` while your NBA stats API says the same game is `nba_stats:0022501234`. Naive joins return nothing. Naive lookups explode silently.

**Warning signs:**
- You look up a game in your DB by external ID and find nothing, yet you know the game happened
- Your odds table has rows that never link to a game row
- Match rates drop below 100% with no error thrown

**Prevention strategy:**
1. Store each API's native ID in a dedicated column: `nba_api_game_id`, `odds_api_game_id`. Never use a single `external_game_id`.
2. Build a deterministic canonical key from stable fields: `{season}_{home_team_abbr}_{away_team_abbr}_{game_date_utc}`. This becomes your cross-API join key — not any external ID.
3. Write a matching service that runs after each ingest and attempts to link unmatched odds rows to game rows using the canonical key. Log all unmatched rows — they reveal API drift.
4. Add a `match_confidence` enum column (`EXACT`, `FUZZY`, `UNMATCHED`) so you know which joins are reliable for calculations.
5. Never silently discard unmatched records. Write them to a `staging_unmatched` table for manual review.

**Phase:** `[Schema]` `[Ingestion]` — must be addressed before any data flows

---

### PITFALL-02: Team Name / Abbreviation Inconsistency Across APIs

**What goes wrong:**
One API calls them `"Boston Celtics"`, another says `"BOS"`, a third says `"Boston"`, the odds feed uses `"boston_celtics"`. Your matching logic breaks whenever you rely on string comparison without normalization. The Oklahoma City Thunder are especially prone to this — some APIs still use legacy names or abbreviations from relocated franchises.

**Warning signs:**
- Canonical key matching fails on specific team matchups
- Your normalization table has gaps

**Prevention strategy:**
1. Build a `team_aliases` lookup table: `(alias TEXT, team_id INT)`. Seed it on day one with every known variant from every API you consume.
2. All ingest paths run team names through the alias resolver before storing. If resolution fails, log it as a hard ingest error — not a warning.
3. Keep the alias table editable at runtime (no code deploy needed to add a new alias).
4. Treat NBA team IDs as the authoritative source; map everything else to those IDs.

**Phase:** `[Schema]` `[Ingestion]`

---

### PITFALL-03: Flaky API Responses With Silent Data Gaps

**What goes wrong:**
Sports APIs — even paid ones — drop data silently. A game finishes but the final score doesn't appear for 45 minutes. An odds line closes but the closing line is missing from the response. Your daily sync job runs at 3 AM, pulls "complete" data, and doesn't know that two games' final scores are pending. You mark them as synced. They never get backfilled.

**Warning signs:**
- Games marked `status = 'final'` in your DB but with null final scores
- Odds records with no closing line despite game being completed
- Discrepancy between your game count and the official NBA schedule count for the day

**Prevention strategy:**
1. Never mark a game as fully ingested based solely on the API's `status` field. Validate: final score exists AND both team scores are non-null AND game date is in the past.
2. Implement a "re-validation window": for 24 hours after a game's scheduled end time, re-query that game's data on every sync run. Use a `needs_verification` flag.
3. Store `last_verified_at` on every game row. Build a dashboard query that surfaces games where `last_verified_at < game_date + 24h AND status != 'verified'`.
4. For odds: store `closing_line_ingested_at`. Alert when this is null more than 2 hours after game tip-off.

**Phase:** `[Ingestion]` `[Sync]`

---

### PITFALL-04: API Rate Limits Causing Partial Sync State

**What goes wrong:**
You hit the rate limit mid-sync. Your job processed games 1–8 out of 12 for the night, then got a 429. You swallow the error, log it, and exit. Tomorrow's sync picks up today's games again but only processes the delta — missing games 9–12 from yesterday. The sync looks "successful" but your data has holes.

**Warning signs:**
- Rate limit errors in logs that are caught but not acted on
- Missing game rows for specific dates with no error surfaced to the user
- Your sync job has no concept of "did we get everything for this date?"

**Prevention strategy:**
1. Design all sync jobs as idempotent, resumable operations. Each game is a unit of work; track its ingestion status independently in a `sync_jobs` table: `(job_id, game_id, status, attempts, last_error, last_attempted_at)`.
2. Implement exponential backoff with jitter for 429s — not just a flat retry.
3. After each sync run, perform a completeness check: compare your game count for the date against the expected count (from the NBA schedule endpoint). If counts differ, raise an alert and mark the job as `PARTIAL`.
4. Pre-schedule API calls to spread load across the allowed window. If you have 500 calls/day, calculate exactly how many calls your sync needs and ensure headroom.
5. Cache the NBA schedule separately — it changes infrequently and burns rate limit quota to re-fetch daily.

**Phase:** `[Sync]` `[Ingestion]`

---

### PITFALL-05: Missing or Delayed Odds Data for Early-Week Games

**What goes wrong:**
NBA games scheduled for Monday sometimes have lines open on Friday. If you only sync "today's games," you'll miss odds that opened days ago. By the time you sync the game day, the opening line is gone or replaced by the current line — and you've lost the historical open.

**Warning signs:**
- Opening line columns are always null for certain games
- Your odds history only shows closing lines, never openers

**Prevention strategy:**
1. Sync odds for all games scheduled in the next 7 days on every run, not just today's games.
2. On first-ever ingest of an odds record for a game, mark `is_opening_line = true` and never overwrite that record — insert a new row for each line movement.
3. Store line history as append-only rows (with timestamps), not updates to a single row.

**Phase:** `[Schema]` `[Sync]`

---

## Part 2: ATS and O/U Calculation Pitfalls

---

### PITFALL-06: Getting ATS Direction Wrong (Home vs. Away Perspective)

**What goes wrong:**
The spread is stored as a single number from one team's perspective. `-6.5` means the favorite gives 6.5 points. But which team is the favorite? If you always apply the spread from the home team's perspective without verifying, you'll flip covers and non-covers for road favorites. Your entire ATS history becomes garbage.

**Warning signs:**
- ATS win rates are clustered around 50% but with suspicious clustering on home/away splits that don't match public records
- "covers" for road favorites look like "losses" and vice versa

**Prevention strategy:**
1. Always store: `favorite_team_id`, `spread` (always positive, from favorite's perspective), `is_home_team_favorite` (boolean). Never store just a signed number ambiguously attributed to "home team."
2. ATS calculation: `favorite_margin = favorite_score - underdog_score`. If `favorite_margin > spread` → favorite covers. If `favorite_margin < spread` → underdog covers. If `favorite_margin == spread` → push.
3. Write unit tests for all four cases: home fav covers, home fav doesn't cover, away fav covers, away fav doesn't cover, and push. Run these against known historical results.
4. Cross-check at least 20 known ATS results against a trusted public source (Action Network, covers.com) before trusting your calculation engine.

**Phase:** `[Schema]` `[Calc]` `[Testing]`

---

### PITFALL-07: Ignoring Push Cases (Exact-Cover Spreads)

**What goes wrong:**
You store spreads as decimals but forget that integer spreads can push. Boston -6 wins by exactly 6. That is a push — bettors get their money back. Many implementations incorrectly count this as a cover or a loss. This corrupts ROI calculations when pushes are in the dataset.

**Warning signs:**
- All ATS records show binary Win/Loss, never Push
- ROI calculations assume -110 juice on every result (pushes return 0, not -10%)

**Prevention strategy:**
1. ATS result must be a three-value enum: `COVER`, `LOSS`, `PUSH`. Never boolean.
2. Half-point spreads (e.g., -6.5) cannot push by definition — this is intentional line shading by sportsbooks to avoid pushes. Your data will have both.
3. When the spread is a whole number, add explicit push-detection logic: `if (favorite_margin == spread) result = PUSH`.
4. O/U push: same logic. A total of `220` with final combined score of `220` is a push. Store as `PUSH`, not `UNDER`.

**Phase:** `[Calc]` `[Schema]`

---

### PITFALL-08: Using Wrong Score for ATS (OT Scores)

**What goes wrong:**
NBA games can go to overtime. ATS bets settle on the final score including overtime. If your API gives you both regulation score and final score separately, and you accidentally use regulation score for ATS calculations, you will have wrong results for every OT game.

**Warning signs:**
- Your ATS results for specific games don't match public records
- Games with OT in the API response consistently show discrepancies

**Prevention strategy:**
1. Explicitly store `home_score_final` and `away_score_final` as the OT-inclusive total. If the API provides both regulation and final, always use final.
2. Store `went_to_overtime BOOLEAN` — useful for filtering and debugging.
3. Add a validation rule: if `home_score_final + away_score_final < (expected_total - 30)`, flag the record as suspect (sanity check for data corruption).

**Phase:** `[Schema]` `[Calc]`

---

### PITFALL-09: Multiple Odds Lines Per Game — Which One Is "The" Spread?

**What goes wrong:**
Your odds API returns lines from multiple sportsbooks: DraftKings -6.5, FanDuel -7, Caesars -6. Which line do you store for ATS calculation? If you store all of them and don't define a canonical line, your ATS results will vary depending on which line you query. Users will get different ATS records depending on a hidden filter.

**Warning signs:**
- Same game appears multiple times in ATS results with different outcomes
- ATS win percentage varies by a few percentage points depending on which query path is used

**Prevention strategy:**
1. Define a canonical sportsbook for ATS calculations and store it in config (not hardcoded). Default to consensus/opening line or a specific book.
2. Store all book lines for display purposes in a separate `odds_lines` table keyed by `(game_id, book_id, recorded_at)`.
3. Store the canonical ATS result in the main `games` table — one row, one result.
4. If the user asks "what line was used?", the `canonical_book` field answers that.

**Phase:** `[Schema]` `[Calc]`

---

### PITFALL-10: Calculating ROI Without Accounting for Juice

**What goes wrong:**
You track ATS win rate but display it as "profit" by assuming even-money bets. Standard NBA spreads are -110 juice (bet $110 to win $100). A 52.38% ATS win rate is the break-even point — not 50%. If your ROI display says "55% win rate = 55% profit," it is misleading users.

**Warning signs:**
- ROI figures don't match what a bettor would have actually earned
- No juice/vig field stored anywhere in the schema

**Prevention strategy:**
1. Store `juice` (or `vig`) per odds record. Most spread lines are -110/-110 but this varies.
2. ROI formula: `ROI = (wins * (100/110)) - (losses * 1) + (pushes * 0)` per unit, not `wins - losses`.
3. Display both ATS win% AND units won/lost separately. Don't conflate them.

**Phase:** `[Schema]` `[Calc]`

---

## Part 3: PostgreSQL Schema Pitfalls

---

### PITFALL-11: Storing Game Times in Local Time Without Timezone

**What goes wrong:**
NBA games tip off at times like `7:30 PM ET`. If you store this as a naive `TIMESTAMP` without timezone, your queries will be wrong when your server's timezone differs from Eastern. Daily sync jobs that filter "today's games" will include yesterday's late games or miss early tip-offs.

**Warning signs:**
- Games appear on the wrong date in queries
- "Tonight's games" includes a game from the previous night

**Prevention strategy:**
1. Always use `TIMESTAMPTZ` (timestamp with time zone) in PostgreSQL. Store everything in UTC. Convert to local time at the display layer only.
2. Aiven PostgreSQL defaults to UTC — verify this on initial connection and enforce it with `SET timezone = 'UTC'` in your connection string.
3. Index on `game_time_utc` for all date-range queries.
4. Document that all API-facing timestamps are ISO 8601 with explicit `Z` suffix.

**Phase:** `[Schema]`

---

### PITFALL-12: Mutable Odds Data Without Audit Trail

**What goes wrong:**
You update an odds record in-place when the line moves. A game's spread started at -4.5, moved to -6.5 by tip-off. You overwrite it. Now you can't track line movement, can't calculate "closing line value," and can't debug why a historical ATS result was calculated the way it was.

**Warning signs:**
- No `created_at` / `updated_at` distinction on odds rows
- No way to answer "what was the opening line for this game?"

**Prevention strategy:**
1. Treat odds data as append-only. Never UPDATE an odds row — INSERT a new one with a timestamp.
2. Use a `is_closing_line BOOLEAN` flag set only after the game tips off.
3. Use a `is_opening_line BOOLEAN` flag set only on the first insert for a game.
4. Add a partial index: `CREATE INDEX ON odds(game_id) WHERE is_closing_line = true` for fast closing-line lookups.

**Phase:** `[Schema]`

---

### PITFALL-13: No Soft-Delete or Status Tracking for Postponed Games

**What goes wrong:**
A game is postponed. You hard-delete it or mark it `status = 'cancelled'` and stop syncing it. Later, the game is rescheduled. You have orphaned odds records, broken foreign keys, and a rescheduled game that looks like a new game. Or worse: you don't detect the postponement at all, and your sync marks it as "upcoming" forever.

**Warning signs:**
- Games with future dates that never get final scores
- Odds records with no matching game after a sync

**Prevention strategy:**
1. Never hard-delete game records. Use `status` with a rich enum: `SCHEDULED`, `IN_PROGRESS`, `FINAL`, `POSTPONED`, `RESCHEDULED`, `CANCELLED`.
2. When a game is postponed, update status to `POSTPONED`, set `original_game_date`, and create a new row when rescheduled with `rescheduled_from_game_id` FK.
3. On every sync run for a game marked `SCHEDULED` that is more than 24 hours past its start time with no final score: auto-flag as `SUSPECT` and alert.
4. Keep odds records linked to the original game ID even after reschedule — they are historically accurate for the original date.

**Phase:** `[Schema]` `[Sync]`

---

### PITFALL-14: Missing Indexes on High-Frequency Query Patterns

**What goes wrong:**
Your schema works fine with 82 games (one season). But your queries for "all ATS results this season by team" or "all O/U results for games where the total was between 225–235" do sequential scans because you didn't add indexes on the columns you filter by most. For a small app this is fine until it isn't — especially if Aiven's free/entry tier has limited resources.

**Prevention strategy:**
1. Index `game_date` on the games table.
2. Index `(home_team_id, game_date)` and `(away_team_id, game_date)` on games.
3. Index `(game_id, book_id)` on odds_lines.
4. Run `EXPLAIN ANALYZE` on every dashboard query before launch.
5. Add `pg_stat_statements` to Aiven config to track slow queries in production.

**Phase:** `[Schema]`

---

## Part 4: Docker and Infrastructure Pitfalls

---

### PITFALL-15: .NET API Can't Reach PostgreSQL Container on Startup

**What goes wrong:**
Docker Compose starts your .NET API container before PostgreSQL is ready to accept connections. The API crashes on startup, Docker restarts it, PostgreSQL is ready by then, and it works — but only by accident. In production (Aiven PostgreSQL), this isn't an issue, but in local dev it causes mysterious startup failures and misleads developers.

**Warning signs:**
- `docker-compose up` sometimes works, sometimes fails, depending on timing
- "Connection refused" errors in .NET logs on first startup

**Prevention strategy:**
1. Add a `healthcheck` to your PostgreSQL service in `docker-compose.yml` using `pg_isready`.
2. Add `depends_on: { db: { condition: service_healthy } }` to your API service.
3. Implement retry logic in your .NET app's startup (`IHostedService` or EF Core migration retry policy) — don't rely solely on Docker orchestration.
4. For Aiven (external DB), store the connection string in `.env` and ensure the container can reach external hosts (no `network: internal` isolation that blocks egress).

**Phase:** `[Infra]`

---

### PITFALL-16: React Dev Server Can't Reach .NET API Due to CORS or Docker Networking

**What goes wrong:**
In Docker Compose, your React dev container tries to call `http://localhost:5000/api` — but from inside the React container, `localhost` is the React container itself, not the .NET API. You get CORS errors or connection refused that look like CORS errors.

**Warning signs:**
- API calls work from the host browser but fail when running inside Docker
- CORS errors in browser console that don't reproduce outside Docker

**Prevention strategy:**
1. Use Docker service names for inter-container communication: `http://api:5000` not `http://localhost:5000`.
2. Configure Vite (or CRA) proxy in `vite.config.ts` to proxy `/api` requests to `http://api:5000` so the browser never makes cross-origin requests.
3. In .NET, configure CORS explicitly: allow the React dev origin (`http://localhost:5173`) in development, lock it down in production.
4. Don't use `host.docker.internal` in production Compose files — it's a development hack.

**Phase:** `[Infra]`

---

### PITFALL-17: Secrets Leaking Into Docker Images or Source Control

**What goes wrong:**
You hardcode the Aiven PostgreSQL connection string, JWT secret key, or API keys in `appsettings.json` or `docker-compose.yml`. These get committed to Git. Aiven connection strings include credentials in the URI.

**Warning signs:**
- Connection strings or API keys appear in any committed file
- `docker inspect` on a running container shows secrets in environment variables readable by anyone with Docker access

**Prevention strategy:**
1. Use `.env` files for all secrets in local dev. Add `.env` to `.gitignore` immediately.
2. Use `docker-compose.yml` with `env_file: .env` — never inline secret values.
3. In production, use environment variables injected at runtime (not baked into the image).
4. For the JWT secret: minimum 256-bit random key. Store in env var `JWT__SecretKey`. Never in `appsettings.json`.
5. Run `git secret` or `truffleHog` scan before first push.

**Phase:** `[Infra]` `[Auth]`

---

### PITFALL-18: Scheduled Sync Job in Docker With No Observability

**What goes wrong:**
Your daily sync job runs as a background `IHostedService` or a cron-triggered container. It runs silently, succeeds silently, fails silently. You discover data is stale three days later when a user reports a missing game.

**Warning signs:**
- No logs surfaced to any monitoring system
- No way to query "when did the last sync run and did it succeed?"

**Prevention strategy:**
1. Write sync job outcomes to a `sync_runs` table: `(id, started_at, completed_at, status, games_processed, errors, notes)`.
2. Expose a `/api/admin/sync-status` endpoint that returns the last sync run record.
3. On failure, write the full exception to `sync_runs.errors` (JSON blob).
4. If using `IHostedService`, wrap the entire sync method in a try/catch and record both success and failure.
5. Add structured logging with Serilog — output to stdout so Docker captures it. Aiven has log forwarding if needed.

**Phase:** `[Sync]` `[Infra]`

---

## Part 5: ASP.NET Core Auth Pitfalls

---

### PITFALL-19: JWT Secret Too Short or Predictable

**What goes wrong:**
You use a JWT secret like `"mySecretKey"` or `"nba-tracker-secret"` in development and forget to rotate it before launch. JWT signing secrets must be unpredictable and at least 256 bits for HS256. A weak secret can be brute-forced from a valid token.

**Prevention strategy:**
1. Generate the JWT secret with: `openssl rand -base64 32` (32 bytes = 256 bits).
2. Never commit the secret. Load from environment: `builder.Configuration["JWT:SecretKey"]`.
3. In `appsettings.Development.json`, put a clearly fake placeholder value that will throw if used in production.
4. Set JWT expiry to something reasonable (15 minutes for access tokens, 7 days for refresh). Do not use 1-year expiry tokens.

**Phase:** `[Auth]`

---

### PITFALL-20: Not Validating JWT Issuer and Audience

**What goes wrong:**
You configure JWT validation to only check the signature, not the `iss` (issuer) or `aud` (audience) claims. A token issued by a different service (even using the same library) could be accepted. More commonly: you forget to set these claims when issuing tokens, so every token has null issuer/audience, and your validation silently passes.

**Prevention strategy:**
1. Set `ValidateIssuer = true`, `ValidateAudience = true` in `TokenValidationParameters`.
2. Store issuer and audience in config. Ensure they match what you set when generating the token.
3. Add an integration test that asserts a token with wrong issuer is rejected with 401.

**Phase:** `[Auth]` `[Testing]`

---

### PITFALL-21: Refresh Token Not Stored Server-Side

**What goes wrong:**
You issue a refresh token as a long-lived JWT (or opaque token) and don't store it anywhere server-side. You can't revoke it. A user logs out but their refresh token is still valid for 7 days. If it's stolen, there's no way to invalidate it.

**Prevention strategy:**
1. Store refresh tokens in a `refresh_tokens` table: `(token_hash, user_id, expires_at, revoked_at, created_at)`. Store the hash, not the plaintext.
2. On refresh: look up the token hash, verify it's not revoked and not expired, issue a new access token, rotate the refresh token (issue new, revoke old).
3. On logout: revoke the refresh token by setting `revoked_at`.

**Phase:** `[Auth]` `[Schema]`

---

### PITFALL-22: Exposing Admin Sync Endpoints Without Authorization

**What goes wrong:**
You add a `/api/sync/trigger` or `/api/admin/resync` endpoint for debugging. You forget to put `[Authorize(Roles = "Admin")]` on it. Anyone who discovers the endpoint can trigger your sync job, burning API rate limits or flooding your database.

**Prevention strategy:**
1. Every admin endpoint gets `[Authorize(Policy = "AdminOnly")]` from day one.
2. Add an integration test that asserts non-authenticated and non-admin requests return 401/403.
3. Prefix all admin routes with `/api/admin/` and apply a route-level authorization policy in middleware.

**Phase:** `[Auth]` `[API]`

---

## Part 6: Postponed Game and Scheduling Pitfalls

---

### PITFALL-23: Sync Job Doesn't Handle Postponements Mid-Run

**What goes wrong:**
Your sync starts at 3 AM. It fetches today's schedule: 8 games. It begins ingesting odds for all 8. Between your schedule fetch and your odds fetch, the NBA postpones Game 3 (rare but happens). The odds API now returns no lines for that game. Your code throws a null reference or silently skips. Game 3 is now in your DB as `SCHEDULED` with no odds, and tomorrow's sync thinks it already processed it.

**Warning signs:**
- Games stuck in `SCHEDULED` status with past dates
- Odds records for exactly N-1 games when N were expected

**Prevention strategy:**
1. Design odds ingestion to tolerate missing odds gracefully: a game with no odds is valid (it may be postponed or too early). Log it; don't error.
2. Re-check the status of every game in your processing queue against the live schedule API before marking it processed. If the API says `postponed`, update status accordingly.
3. Your completeness check (from PITFALL-04) will surface the discrepancy.

**Phase:** `[Sync]` `[Ingestion]`

---

### PITFALL-24: Calculating ATS on a Rescheduled Game With Wrong Odds

**What goes wrong:**
Game postponed, rescheduled 2 weeks later. The original odds were -4.5 home. New odds open at -2. You stored odds from the original date. Your ATS calculation uses the wrong spread for the game that actually played.

**Prevention strategy:**
1. When a game is rescheduled, create a new game record for the new date. Do not reuse the old game record.
2. Ingest fresh odds for the new game record. The old odds stay attached to the old (postponed) game record for historical accuracy.
3. ATS calculations only run on games with `status = 'FINAL'` — the postponed game never gets an ATS result.

**Phase:** `[Schema]` `[Calc]` `[Sync]`

---

## Quick Reference: Pitfall-to-Phase Mapping

| Phase | Pitfalls |
|-------|----------|
| Schema | 01, 02, 05, 06, 07, 08, 09, 11, 12, 13, 14, 21, 24 |
| Ingestion | 01, 02, 03, 04, 05, 23 |
| Sync | 03, 04, 05, 13, 18, 23, 24 |
| Calc | 06, 07, 08, 09, 10, 24 |
| Auth | 17, 19, 20, 21, 22 |
| Infra | 15, 16, 17, 18 |
| API | 22 |
| Testing | 06, 20, 22 |

---

## Top 5 "Build This First" Preventions

These are the highest-impact items that, if skipped in early phases, cause cascading failures that are expensive to fix later:

1. **Dual external ID columns + canonical key** (PITFALL-01) — everything downstream depends on cross-API matching
2. **Append-only odds with `is_opening_line` / `is_closing_line`** (PITFALL-12) — cannot reconstruct line history if overwritten
3. **ATS result as 3-value enum with push detection** (PITFALL-07) — binary ATS results corrupt all analytics
4. **TIMESTAMPTZ everywhere** (PITFALL-11) — timezone bugs are silent and affect every query
5. **Sync outcomes tracked in `sync_runs` table** (PITFALL-18) — no observability = no confidence in data freshness

---

*Generated by GSD Project Researcher for NBA Lines Tracker — Pitfalls Dimension*
*Date: 2026-02-17*
