import type { GameLogEntry } from '@/types/api'

export type StreakResult = { type: 'W' | 'L'; count: number } | null

/**
 * Computes streak from a game log array sorted descending by date (most recent first).
 * Used in team panels where game log is fetched independently.
 * isHomeGame on GameLogEntry already encodes the team's perspective.
 */
export function computeStreakFromGames(games: GameLogEntry[]): StreakResult {
  if (games.length === 0) return null

  const first = games[0]
  const won = first.isHomeGame
    ? (first.homeScore ?? 0) > (first.awayScore ?? 0)
    : (first.awayScore ?? 0) > (first.homeScore ?? 0)
  const streakType: 'W' | 'L' = won ? 'W' : 'L'

  let count = 0
  for (const game of games) {
    const gameWon = game.isHomeGame
      ? (game.homeScore ?? 0) > (game.awayScore ?? 0)
      : (game.awayScore ?? 0) > (game.homeScore ?? 0)
    if ((gameWon && streakType === 'W') || (!gameWon && streakType === 'L')) {
      count++
    } else {
      break
    }
  }

  return { type: streakType, count }
}
