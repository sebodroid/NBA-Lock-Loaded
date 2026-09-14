import { useQuery } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import { apiClient } from './client'
import type { InsightResponse } from '@/types/api'

export function useInsights() {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['insights', sport],
    queryFn: () => apiClient.get<InsightResponse[]>(`/api/${sport}/insights`).then(r => r.data),
    staleTime: 60 * 1000,
  })
}
