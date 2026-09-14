import { useQuery } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import { apiClient } from './client'
import type { HotBetEntry } from '@/types/api'

export function useHotBets(limit = 10) {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['hot-bets', sport, limit],
    queryFn: () =>
      apiClient.get<HotBetEntry[]>(`/api/${sport}/hot-bets`, { params: { limit } }).then(r => r.data),
    staleTime: 5 * 60 * 1000,
  })
}
