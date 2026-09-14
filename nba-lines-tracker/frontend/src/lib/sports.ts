export const SPORTS = ['nba', 'nfl', 'mlb'] as const
export type Sport = typeof SPORTS[number]

export const SPORT_LABELS: Record<Sport, string> = {
  nba: 'NBA',
  nfl: 'NFL',
  mlb: 'MLB',
}

export function isSport(value: string | undefined): value is Sport {
  return !!value && (SPORTS as readonly string[]).includes(value)
}

// Conference filter options per sport — NFL has no conference data from its
// data source yet, so the filter is hidden entirely rather than shown broken.
export const CONFERENCE_OPTIONS: Record<Sport, readonly string[]> = {
  nba: ['East', 'West'],
  mlb: ['American League', 'National League'],
  nfl: [],
}
