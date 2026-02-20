import { useQuery } from '@tanstack/react-query'
import { apiClient } from './client'
import type { TeamStatsResponse, TeamDetailResponse, GameLogEntry } from '@/types/api'

export function useTeams() {
  return useQuery({
    queryKey: ['teams'],
    queryFn: () => apiClient.get<TeamStatsResponse[]>('/api/teams').then(r => r.data),
    staleTime: 60 * 1000,
  })
}

export function useTeamStats(teamId: number | null) {
  return useQuery({
    queryKey: ['team-stats', teamId],
    queryFn: () => apiClient.get<TeamDetailResponse>(`/api/teams/${teamId}/stats`).then(r => r.data),
    enabled: teamId !== null,
    staleTime: 60 * 1000,
  })
}

export function useTeamGames(teamId: number | null) {
  return useQuery({
    queryKey: ['team-games', teamId],
    queryFn: () => apiClient.get<GameLogEntry[]>(`/api/teams/${teamId}/games`).then(r => r.data),
    enabled: teamId !== null,
    staleTime: 60 * 1000,
  })
}
