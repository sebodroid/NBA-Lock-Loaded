// Mirrors the backend's PropMarketMapping.cs — the markets this app knows how to grade
// automatically (auto-tracked or manual). Picking one of these for a manual bet means
// it gets graded off the real box score just like a synced prop, not left Pending forever.
export interface PropMarketOption {
  key: string
  label: string
  category: 'Receiving' | 'Passing' | 'Rushing' | 'Touchdowns'
}

// Defense markets (Sacks, Solo Tackles) are deliberately left out — Highlightly doesn't
// provide defensive box-score stats at all yet (confirmed via a live stat-names query),
// so picking one here would always show "no data," not just for a specific player. Add
// them back once that data actually starts flowing.
export const PROP_MARKETS: PropMarketOption[] = [
  { key: 'player_reception_yds',     label: 'Receiving Yards',     category: 'Receiving' },
  { key: 'player_receptions',        label: 'Receptions',          category: 'Receiving' },
  { key: 'player_reception_longest', label: 'Longest Reception',   category: 'Receiving' },
  { key: 'player_pass_yds',          label: 'Passing Yards',       category: 'Passing' },
  { key: 'player_pass_tds',          label: 'Passing TDs',         category: 'Passing' },
  { key: 'player_pass_completions',  label: 'Completions',         category: 'Passing' },
  { key: 'player_rush_yds',          label: 'Rushing Yards',       category: 'Rushing' },
  { key: 'player_rush_attempts',     label: 'Rushing Attempts',    category: 'Rushing' },
  { key: 'player_rush_longest',      label: 'Longest Rush',        category: 'Rushing' },
  { key: 'player_anytime_td',        label: 'Anytime TD',          category: 'Touchdowns' },
]
