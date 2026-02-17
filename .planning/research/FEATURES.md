# Feature Research

**Domain:** NBA Betting Performance Tracker (ATS / Over-Under analytics)
**Researched:** 2026-02-17
**Confidence:** HIGH

## Feature Landscape

### Table Stakes (Users Expect These)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| All 30 teams displayed with logo/name | It's an NBA app — teams are the product | LOW | Use team color schemes for visual identity |
| Season W-L record per team | First thing anyone checks | LOW | Pulled from NBA data API |
| Current W/L streak | Tells you momentum at a glance | LOW | Calculate from last N game results |
| ATS record (covered/missed/%) | Core value of the app | MEDIUM | Requires matching game results to spread lines |
| O/U record (over/under/push/%) | Core value of the app | MEDIUM | Requires matching game totals to total lines |
| Sortable columns | Users immediately want to rank teams by ATS% | LOW | Standard data grid behavior |
| Filter by conference (East/West) | Half the league is irrelevant to many users | LOW | Simple enum filter |
| Filter by division | Narrower grouping, often useful | LOW | Simple enum filter |
| Last N games stats (e.g., last 10) | Recency matters more than full season for trends | MEDIUM | Requires date-ranged queries |
| Home / Away split stats | Teams often cover more at home; users expect this | MEDIUM | Split ATS/OU records by home/away |
| Per-user login | Required for invite-only access | MEDIUM | JWT auth; individual accounts |
| Data freshness indicator | Users need to know when data was last updated | LOW | "Last synced: 2h ago" timestamp |

### Differentiators (Competitive Advantage)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Multi-panel side-by-side comparison | Compare two teams simultaneously without losing context | HIGH | Each panel is an independent component with its own state; dock/float UI |
| Customizable column visibility | Users have different priorities; let them hide noise | MEDIUM | Column config stored per-user in DB or localStorage |
| ATS trend line (last 10 vs season) | Spot teams on a covering run vs cold streak | MEDIUM | Simple sparkline chart or colored trend indicator |
| Cover % by favorite vs underdog split | Teams cover differently when favored vs dog | MEDIUM | Requires storing line value to determine favorite |
| Over/under split by game total (low/high totals) | Some teams hit overs only in low-total games | MEDIUM | Bucket totals (e.g., <215, 215-225, >225) |
| Quick-filter presets | "Show me best ATS teams last 10 games" one click | LOW | Saved filter combinations |
| Color-coded performance indicators | Green/red for good/bad ATS% at a glance | LOW | Threshold-based coloring (e.g., <50% red, >55% green) |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Live in-game odds updates | Feels exciting, "real-time" | WebSocket complexity + API costs spike; game-day data is noisy and unusable for trend analysis | Daily batch after games complete — cleaner data, correct final lines |
| Win probability / prediction model | Sounds powerful | Requires ML infrastructure, training data, ongoing calibration — scope explosion | Surface raw ATS/OU stats and let users draw their own conclusions |
| Alerts / push notifications | "Notify me when a team covers" | Email/push infra complexity; notification fatigue | v2 feature if demand is there |
| Social features (comments, picks) | Adds engagement | Significant auth/moderation complexity; not the core value | Keep it a tracker, not a social network |
| Player prop tracking (v1) | Natural extension | Doubles the data model complexity and API surface area | Explicitly deferred to v2 — confirmed by user |
| Betting line shopping (compare books) | Useful for sharp bettors | Requires multiple odds API subscriptions | Single odds source sufficient for v1 |

## Feature Dependencies

```
Per-user auth
    └──required by──> Column customization (per-user preferences)
    └──required by──> Saved filter presets

NBA data ingestion (game scores + schedules)
    └──required by──> W-L record
    └──required by──> Streak calculation
    └──required by──> ATS result calculation
    └──required by──> O/U result calculation

Odds data ingestion (spread + totals lines)
    └──required by──> ATS record (covered/missed/%)
    └──required by──> O/U record (over/under/push/%)
    └──required by──> Cover % by favorite/underdog split

ATS + O/U records
    └──enhances──> Multi-panel comparison (the data displayed in panels)
    └──enhances──> Trend lines (last 10 vs season)

Home/Away split
    └──requires──> Game location stored on each game record
```

### Dependency Notes

- **Auth required for column customization:** User preferences need a user identity to persist against
- **Both APIs required before any betting stats:** Cannot calculate ATS/OU without both game result AND what the line was
- **Trend lines require date ordering:** Game records must have dates; calculate rolling windows in queries

## MVP Definition

### Launch With (v1)

- [ ] All 30 teams with record, streak, ATS%, O/U% — core value delivered
- [ ] Sortable, filterable data grid (conference, division, column sort) — data usable
- [ ] Multi-panel team comparison — the differentiating UX
- [ ] Customizable column visibility — reduces noise for each user
- [ ] Home/Away split stats — first cut filter users will want
- [ ] Last 10 games ATS/OU — recency matters
- [ ] Per-user login — required for invite-only access
- [ ] Data freshness timestamp — trust signal

### Add After Validation (v1.x)

- [ ] ATS trend sparklines — add when users ask "is this team on a run?"
- [ ] Cover % by favorite/underdog — add when users start slicing data deeper
- [ ] Quick-filter presets — add when users are repeatedly setting the same filters

### Future Consideration (v2+)

- [ ] Player prop tracking (confirmed deferred by user)
- [ ] Alerts / notifications
- [ ] ATS by game total bucket (advanced analysis)
- [ ] Multi-season historical comparison

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| ATS record per team | HIGH | MEDIUM | P1 |
| O/U record per team | HIGH | MEDIUM | P1 |
| Season record + streak | HIGH | LOW | P1 |
| Data grid sort/filter | HIGH | LOW | P1 |
| Multi-panel comparison | HIGH | HIGH | P1 |
| Per-user auth | HIGH | MEDIUM | P1 |
| Home/Away split | MEDIUM | MEDIUM | P1 |
| Last 10 games filter | HIGH | MEDIUM | P1 |
| Column customization | MEDIUM | LOW | P1 |
| Data freshness indicator | MEDIUM | LOW | P1 |
| ATS trend sparklines | MEDIUM | MEDIUM | P2 |
| Favorite/underdog split | MEDIUM | MEDIUM | P2 |
| Quick-filter presets | LOW | LOW | P2 |
| Player props | HIGH | HIGH | P3 (v2) |

## Competitor Feature Analysis

| Feature | SportsReference/Pro-FB-Ref | Covers.com | Our Approach |
|---------|--------------------------|------------|--------------|
| ATS records | Tabular, full historical | ATS records + trends | Current season focus, cleaner UI |
| O/U records | Available | Available | Both, with push tracking |
| Side-by-side comparison | Not native | Not native | Multi-panel — our differentiator |
| Custom columns | No | No | Yes — per-user |
| Auth / private | Public | Public | Invite-only friend group |
| Home/away split | Yes | Yes | Yes — table stakes |

## Sources

- Sports Reference (basketball-reference.com) — ATS/OU feature conventions
- Covers.com — betting tracker UI patterns
- The Action Network — trend/recency patterns users rely on
- User interview context: friend group use case, multi-panel comparison as explicit request

---
*Feature research for: NBA Betting Performance Tracker*
*Researched: 2026-02-17*
