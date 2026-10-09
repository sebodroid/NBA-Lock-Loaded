import { useQuery } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import { apiClient } from './client'
import type { TodayMatchupResponse, GamePreviewResponse, GamePropEntry, GameListEntry, GamePlayerEntry } from '@/types/api'

export function useTodayMatchups() {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['games', 'today', sport],
    queryFn: () => apiClient.get<TodayMatchupResponse[]>(`/api/${sport}/games/today`).then(r => r.data),
    staleTime: 2 * 60 * 1000,  // 2 min — lines and status update throughout the day
  })
}

// Every player prop line tied to this game — the direct "what can I bet on" view for
// a matchup card, so props are visible even before a player shows up in a leaderboard.
export function useGameProps(gameId: number | null) {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['games', 'props', sport, gameId],
    queryFn: () => apiClient.get<GamePropEntry[]>(`/api/${sport}/games/${gameId}/props`).then(r => r.data),
    enabled: gameId !== null && gameId > 0,
    staleTime: 2 * 60 * 1000,
  })
}

// Games within +/- `days` of today — for the manual-bet game picker, which needs to
// find a bet already played this week just as easily as one still upcoming.
export function useRecentGames(days = 10) {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['games', 'recent', sport, days],
    queryFn: () => apiClient.get<GameListEntry[]>(`/api/${sport}/games/recent`, { params: { days } }).then(r => r.data),
    staleTime: 60 * 1000,
  })
}

// Every player on either roster for a game — for the manual player-prop bet picker.
export function useGamePlayers(gameId: number | null) {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['games', 'players', sport, gameId],
    queryFn: () => apiClient.get<GamePlayerEntry[]>(`/api/${sport}/games/${gameId}/players`).then(r => r.data),
    enabled: gameId !== null && gameId > 0,
    staleTime: 5 * 60 * 1000,
  })
}

// Generated on demand (button click) — Claude calls have real cost and latency,
// so this never fetches automatically, only via the returned refetch().
export function useGamePreview(gameId: number) {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['game-preview', sport, gameId],
    queryFn: () => apiClient.get<GamePreviewResponse>(`/api/${sport}/games/${gameId}/preview`).then(r => r.data),
    enabled: false,
    staleTime: 60 * 60 * 1000,
    retry: false,
  })
}
