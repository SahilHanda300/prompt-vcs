import { useQuery } from '@tanstack/react-query'
import { API_URL } from '../lib/api'
import type { SiteDashboardItem, QAFailureItem, DegradedTestCase } from '../types'

export function usePRODDashboard() {
  return useQuery<SiteDashboardItem[]>({
    queryKey: ['prod-dashboard'],
    queryFn: async () => {
      const res = await fetch(`${API_URL}/sites/dashboard`)
      if (!res.ok) throw new Error('Failed to fetch dashboard')
      return res.json()
    },
    refetchInterval: 30_000,
  })
}

export function useQAFailures() {
  return useQuery<QAFailureItem[]>({
    queryKey: ['qa-failures'],
    queryFn: async () => {
      const res = await fetch(`${API_URL}/eval/failures`)
      if (!res.ok) throw new Error('Failed to fetch QA failures')
      return res.json()
    },
    refetchInterval: 60_000,
  })
}

export function useDegradedCases(evalId: string, enabled: boolean) {
  return useQuery<DegradedTestCase[]>({
    queryKey: ['degraded-cases', evalId],
    queryFn: async () => {
      const res = await fetch(`${API_URL}/eval/failures/${evalId}/cases`)
      if (!res.ok) throw new Error('Failed to fetch test cases')
      return res.json()
    },
    enabled,
    staleTime: Infinity,
  })
}
