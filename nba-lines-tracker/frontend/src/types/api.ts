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
  category: 'team-ats' | 'team-ou' | 'market-trend'
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
  bookmaker: string            // Over side's best-priced book
  underBookmaker: string | null // Under side's best-priced book — can differ from bookmaker
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

// "Questionable" | "Doubtful" | "Out" | "Injured Reserve" — null means not on the report.
export type InjuryStatus = string | null

export interface PlayerCardResponse {
  playerId: number
  name: string
  teamAbbreviation: string | null
  gamesPlayed: number
  seasonStats: Record<string, number>
  upcomingProps: PropEstimate[]
  injuryStatus: InjuryStatus
  injuryNote: string | null
}

// One player's prop line within the context of a specific game — the direct
// "what can I bet on tonight" view, shown on the matchup card itself rather than
// requiring the player to already appear in a leaderboard.
export interface GamePropEntry {
  playerId: number
  playerName: string
  teamAbbreviation: string | null
  estimate: PropEstimate
  injuryStatus: InjuryStatus
  injuryNote: string | null
}

export interface BetResponse {
  id: number
  kind: 'PlayerProp' | 'Spread' | 'Total'
  isManual: boolean            // placed on another book, logged here by hand
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
  closingLine: number | null
  closingOdds: number | null
  clvPct: number | null       // + = beat the closing line, - = line moved against you
}

export interface CreateBetRequest {
  gameId: number
  kind: 'PlayerProp' | 'Spread' | 'Total'
  side: 'Over' | 'Under' | 'Home' | 'Away'
  stakeAmount: number
  // Auto mode: reference a line this app actually synced.
  playerPropLineId?: number | null
  // Manual mode: the line/odds you actually got, placed on another book.
  manualLine?: number | null
  manualOdds?: number | null
  playerId?: number | null      // PlayerProp manual mode
  marketKey?: string | null     // PlayerProp manual mode — e.g. "player_receptions"
  marketLabel?: string | null   // manual mode — overrides the derived label
}

// A game in the manual-bet picker's range (recent + upcoming) — not the full
// TodayMatchupResponse shape, just enough to pick the right one.
export interface GameListEntry {
  gameId: number
  gameDate: string
  status: string
  homeTeamId: number
  homeTeamAbbr: string
  homeTeamName: string
  awayTeamId: number
  awayTeamAbbr: string
  awayTeamName: string
}

// One roster entry for the manual player-prop bet picker.
export interface GamePlayerEntry {
  playerId: number
  name: string
  teamAbbreviation: string | null
}

export interface HotBetEntry {
  playerId: number
  playerName: string
  teamAbbreviation: string | null
  gameId: number
  gameLabel: string
  gameDate: string
  estimate: PropEstimate
  injuryStatus: InjuryStatus
  injuryNote: string | null
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
