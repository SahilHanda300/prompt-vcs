import { useQuery } from '@tanstack/react-query'
import { API_URL } from '../lib/api'
import { useAuth } from '../lib/AuthContext'
import type { SiteDashboardItem, QAFailureItem, DegradedTestCase } from '../types'

export function usePRODDashboard() {
  const { user } = useAuth()

  return useQuery<SiteDashboardItem[]>({
    queryKey: ['prod-dashboard', user?.username],
    queryFn: async () => {
      const url = user?.username
        ? `${API_URL}/sites/dashboard?username=${encodeURIComponent(user.username)}`
        : `${API_URL}/sites/dashboard`
      const res = await fetch(url)
      if (!res.ok) throw new Error('Failed to fetch dashboard')
      return res.json()
    },
    enabled: !!user,
    refetchInterval: 30_000,
  })
}

export function useQAFailures() {
  const { user } = useAuth()

  return useQuery<QAFailureItem[]>({
    queryKey: ['qa-failures', user?.username],
    queryFn: async () => {
      const url = user?.username
        ? `${API_URL}/eval/failures?username=${encodeURIComponent(user.username)}`
        : `${API_URL}/eval/failures`
      const res = await fetch(url)
      if (!res.ok) throw new Error('Failed to fetch QA failures')
      return res.json()
    },
    enabled: !!user,
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
