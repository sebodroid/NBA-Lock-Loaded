import { useQuery } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import { apiClient } from './client'
import type { ParlaySuggestion } from '@/types/api'

export function useParlays(limit = 5, legSize = 2) {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['parlays', sport, limit, legSize],
    queryFn: () =>
      apiClient.get<ParlaySuggestion[]>(`/api/${sport}/parlays`, { params: { limit, legSize } }).then(r => r.data),
    staleTime: 5 * 60 * 1000,
  })
}
