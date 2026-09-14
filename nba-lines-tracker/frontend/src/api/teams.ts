import { useQuery } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import { apiClient } from './client'
import type { TeamStatsResponse, TeamDetailResponse, GameLogEntry } from '@/types/api'

// season omitted = live current season; pass e.g. "2025" for a completed past season
export function useTeams(season?: string) {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['teams', sport, season ?? 'current'],
    queryFn: () => apiClient
      .get<TeamStatsResponse[]>(`/api/${sport}/teams`, { params: season ? { season } : undefined })
      .then(r => r.data),
    staleTime: 60 * 1000,
  })
}

export function useTeamStats(teamId: number | null) {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['team-stats', sport, teamId],
    queryFn: () => apiClient.get<TeamDetailResponse>(`/api/${sport}/teams/${teamId}/stats`).then(r => r.data),
    enabled: teamId !== null,
    staleTime: 60 * 1000,
  })
}

export function useTeamGames(teamId: number | null) {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['team-games', sport, teamId],
    queryFn: () => apiClient.get<GameLogEntry[]>(`/api/${sport}/teams/${teamId}/games`).then(r => r.data),
    enabled: teamId !== null,
    staleTime: 60 * 1000,
  })
}
