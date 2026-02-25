import { useQuery } from '@tanstack/react-query'
import { apiClient } from './client'
import type { TodayMatchupResponse } from '@/types/api'

export function useTodayMatchups() {
  return useQuery({
    queryKey: ['games', 'today'],
    queryFn: () => apiClient.get<TodayMatchupResponse[]>('/api/games/today').then(r => r.data),
    staleTime: 2 * 60 * 1000,  // 2 min — lines and status update throughout the day
  })
}
