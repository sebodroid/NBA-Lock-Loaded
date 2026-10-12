import { useQuery } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import { apiClient } from './client'
import type { PropEstimate } from '@/types/api'

export interface PlayerSearchResult {
  id: number
  name: string
  teamAbbreviation: string | null
}

export function usePlayerSearch(query: string) {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['player-search', sport, query],
    queryFn: () =>
      apiClient.get<PlayerSearchResult[]>(`/api/${sport}/players/search`, { params: { q: query } }).then(r => r.data),
    enabled: query.trim().length >= 2,
    staleTime: 60 * 1000,
  })
}

export function useCustomEstimate(playerId: number | null, marketKey: string | null, line: number | null) {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['custom-line', sport, playerId, marketKey, line],
    queryFn: () =>
      apiClient.get<PropEstimate>(`/api/${sport}/custom-line`, {
        params: { playerId, marketKey, line },
      }).then(r => r.data),
    enabled: playerId !== null && marketKey !== null && line !== null && !Number.isNaN(line),
    staleTime: 60 * 1000,
  })
}
