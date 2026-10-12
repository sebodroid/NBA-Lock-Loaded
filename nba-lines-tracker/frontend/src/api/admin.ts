import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from './client'
import type { PendingUser } from '@/types/api'

export function usePendingUsers() {
  return useQuery({
    queryKey: ['admin', 'pending-users'],
    queryFn: () => apiClient.get<PendingUser[]>('/api/admin/users/pending').then(r => r.data),
    staleTime: 30 * 1000,
  })
}

export function useApproveUser() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => apiClient.post(`/api/admin/users/${id}/approve`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'pending-users'] }),
  })
}

export function useRejectUser() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => apiClient.delete(`/api/admin/users/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'pending-users'] }),
  })
}
