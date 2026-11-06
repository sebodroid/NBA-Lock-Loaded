import { useQuery } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import { apiClient } from './client'

export interface AccuracyBucket {
  range: string
  graded: number
  hits: number
  hitRatePct: number | null
}

export interface AccuracyResponse {
  totalSnapshots: number
  gradedSnapshots: number
  pendingSnapshots: number
  overallHits: number
  overallHitRatePct: number | null
  buckets: AccuracyBucket[]
}

export function useAccuracy() {
  const { sport = 'nba' } = useParams<{ sport: string }>()
  return useQuery({
    queryKey: ['accuracy', sport],
    queryFn: () => apiClient.get<AccuracyResponse>(`/api/${sport}/accuracy`).then(r => r.data),
    staleTime: 5 * 60 * 1000,
  })
}
