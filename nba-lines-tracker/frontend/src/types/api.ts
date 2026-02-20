export interface TeamStatsResponse {
  teamId: number
  name: string
  abbreviation: string
  conference: string | null
  division: string | null
  gamesPlayed: number
  wins: number
  losses: number
  atsCovers: number
  atsLosses: number
  atsPushes: number
  ouOvers: number
  ouUnders: number
  ouPushes: number
  streak: number           // positive = W streak, negative = L streak, 0 = no games
  lastSyncedAt: string | null  // ISO 8601 UTC, from SyncRuns
}

export interface HomeAwaySplit {
  gamesPlayed: number
  wins: number
  losses: number
  atsCovers: number
  atsLosses: number
  atsPushes: number
  ouOvers: number
  ouUnders: number
  ouPushes: number
}

export interface TeamDetailResponse {
  teamId: number
  name: string
  abbreviation: string
  conference: string | null
  division: string | null
  home: HomeAwaySplit
  away: HomeAwaySplit
}

export interface GameLogEntry {
  gameId: number
  gameDate: string          // "YYYY-MM-DD"
  homeTeamAbbr: string
  awayTeamAbbr: string
  homeScore: number | null
  awayScore: number | null
  isHomeGame: boolean
  spreadLine: number | null
  totalLine: number | null
  atsResult: 'Cover' | 'Loss' | 'Push' | null
  ouResult: 'Over' | 'Under' | 'Push' | null
}

// Derived computations (pushes excluded from denominator per user decision)
export function calcAtsPct(stats: { atsCovers: number; atsLosses: number }): number | null {
  const total = stats.atsCovers + stats.atsLosses
  if (total === 0) return null
  return (stats.atsCovers / total) * 100
}

export function calcOuPct(stats: { ouOvers: number; ouUnders: number }): number | null {
  const total = stats.ouOvers + stats.ouUnders
  if (total === 0) return null
  return (stats.ouOvers / total) * 100
}

export function formatStreak(streak: number): string {
  if (streak === 0) return '\u2013'
  return streak > 0 ? `W${streak}` : `L${Math.abs(streak)}`
}
