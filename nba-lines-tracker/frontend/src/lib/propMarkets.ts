// Mirrors the backend's PropMarketMapping.cs — the markets this app knows how to grade
// automatically (auto-tracked or manual). Picking one of these for a manual bet means
// it gets graded off the real box score just like a synced prop, not left Pending forever.
export interface PropMarketOption {
  key: string
  label: string
  category: 'Receiving' | 'Passing' | 'Rushing' | 'Touchdowns' | 'Defense'
}

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
  { key: 'player_sacks',             label: 'Sacks',                category: 'Defense' },
  { key: 'player_solo_tackles',      label: 'Solo Tackles',        category: 'Defense' },
]
