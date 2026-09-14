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

export interface TeamSeasonStats {
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

export interface H2HGameEntry {
  gameId: number
  gameDate: string         // "YYYY-MM-DD"
  homeTeamId: number
  homeScore: number | null
  awayScore: number | null
  spreadLine: number | null
  totalLine: number | null
  homeAtsResult: 'Cover' | 'Loss' | 'Push' | null
  awayAtsResult: 'Cover' | 'Loss' | 'Push' | null
  ouResult: 'Over' | 'Under' | 'Push' | null
}

export interface TodayMatchupResponse {
  gameId: number
  status: string           // "SCHEDULED" | "LIVE" | "FINAL"
  homeTeamId: number
  homeTeamName: string
  homeTeamAbbr: string
  awayTeamId: number
  awayTeamName: string
  awayTeamAbbr: string
  spreadLine: number | null
  favoriteTeamId: number | null
  homeSpreadOdds: number | null
  awaySpreadOdds: number | null
  totalLine: number | null
  overOdds: number | null
  underOdds: number | null
  bookmaker: string | null
  homeStats: TeamSeasonStats
  awayStats: TeamSeasonStats
  headToHead: H2HGameEntry[]
}

export interface LeaderboardEntryResponse {
  playerId: number
  name: string
  teamAbbreviation: string | null
  gamesPlayed: number
  stats: Record<string, number>   // stat name (verbatim from data source) -> summed value this season
}

export interface InsightResponse {
  category: 'team-ats' | 'team-ou' | 'team-streak' | 'player-leader'
  text: string
  sampleSize: number   // games behind this insight — always show alongside it
}

export interface GamePreviewResponse {
  text: string
}

export interface PropEstimate {
  propLineId: number
  marketKey: string
  marketLabel: string
  category: string                 // "Passing" | "Rushing" | "Receiving" | "Defense" | "Other"
  line: number
  overOdds: number | null
  underOdds: number | null
  bookmaker: string
  opponentAbbreviation: string
  statBasis: 'current' | 'prior'   // which season the history numbers are drawn from
  gamesWithData: number
  seasonAverage: number | null
  hitCount: number
  hitRatePct: number | null
  opponentAllowedAverage: number | null
  leagueAllowedAverage: number | null
  estimatedHitRatePct: number | null
}

export interface PlayerCardResponse {
  playerId: number
  name: string
  teamAbbreviation: string | null
  gamesPlayed: number
  seasonStats: Record<string, number>
  upcomingProps: PropEstimate[]
}

// One player's prop line within the context of a specific game — the direct
// "what can I bet on tonight" view, shown on the matchup card itself rather than
// requiring the player to already appear in a leaderboard.
export interface GamePropEntry {
  playerId: number
  playerName: string
  teamAbbreviation: string | null
  estimate: PropEstimate
}

export interface BetResponse {
  id: number
  kind: 'PlayerProp' | 'Spread' | 'Total'
  gameId: number
  gameLabel: string           // "SF @ LAR"
  gameDate: string            // "YYYY-MM-DD"
  gameStatus: string
  playerId: number | null
  playerName: string | null
  teamAbbreviation: string | null
  marketLabel: string
  lineAtBet: number
  side: 'Over' | 'Under' | 'Home' | 'Away'
  oddsAtBet: number
  stakeAmount: number
  toWinAmount: number
  outcome: 'Pending' | 'Won' | 'Lost' | 'Push'
  actualValue: number | null
  placedAt: string            // ISO 8601
}

export interface CreateBetRequest {
  gameId: number
  kind: 'PlayerProp' | 'Spread' | 'Total'
  playerPropLineId?: number | null
  side: 'Over' | 'Under' | 'Home' | 'Away'
  stakeAmount: number
}

export interface HotBetEntry {
  playerId: number
  playerName: string
  teamAbbreviation: string | null
  gameId: number
  gameLabel: string
  gameDate: string
  estimate: PropEstimate
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
  if (!streak || !isFinite(streak)) return '\u2013'
  return streak > 0 ? `W${streak}` : `L${Math.abs(streak)}`
}
