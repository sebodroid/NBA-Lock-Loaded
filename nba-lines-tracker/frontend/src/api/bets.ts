import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import { apiClient } from './client'
import type { BetResponse, CreateBetRequest } from '@/types/api'

export function useBets() {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['bets', sport],
    queryFn: () => apiClient.get<BetResponse[]>(`/api/${sport}/bets`).then(r => r.data),
    // Short stale time — outcomes flip from Pending to Won/Lost as games go final, and
    // this is the page you'd actually be watching while that happens.
    staleTime: 30 * 1000,
  })
}

export function useCreateBet() {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (req: CreateBetRequest) =>
      apiClient.post<BetResponse>(`/api/${sport}/bets`, req).then(r => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['bets', sport] }),
  })
}

export function useDeleteBet() {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => apiClient.delete(`/api/${sport}/bets/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['bets', sport] }),
  })
}
