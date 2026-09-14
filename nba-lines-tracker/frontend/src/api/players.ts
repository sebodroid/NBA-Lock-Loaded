import { useQuery } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import { apiClient } from './client'
import type { LeaderboardEntryResponse, PlayerCardResponse } from '@/types/api'

// season omitted = live current season; pass e.g. "2025" for a completed past season
export function useLeaderboard(category: string, season?: string) {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['leaderboard', sport, category, season ?? 'current'],
    queryFn: () =>
      apiClient
        .get<LeaderboardEntryResponse[]>(
          `/api/${sport}/leaderboards/${category}`,
          { params: season ? { season } : undefined }
        )
        .then(r => r.data),
    staleTime: 60 * 1000,
  })
}

export function usePlayerCard(playerId: number | null) {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['player-card', sport, playerId],
    queryFn: () => apiClient.get<PlayerCardResponse>(`/api/${sport}/players/${playerId}`).then(r => r.data),
    enabled: playerId !== null,
    staleTime: 60 * 1000,
  })
}
