import { useQuery } from '@tanstack/react-query'
import { API_URL } from '../lib/api'
import type { SiteListItem } from '../types'

export function usePRODSites() {
  return useQuery<SiteListItem[]>({
    queryKey: ['prod-sites'],
    queryFn: async () => {
      const res = await fetch(`${API_URL}/sites`)
      if (!res.ok) throw new Error('Failed to fetch sites')
      return res.json()
    },
    refetchInterval: 30_000,
    staleTime: 25_000,
  })
}
