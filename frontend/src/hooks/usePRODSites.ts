import { useQuery } from '@tanstack/react-query'
import { API_URL } from '../lib/api'
import { useAuth } from '../lib/AuthContext'
import type { SiteListItem } from '../types'

export function usePRODSites() {
  const { user } = useAuth()

  return useQuery<SiteListItem[]>({
    queryKey: ['prod-sites', user?.username],
    queryFn: async () => {
      const url = user?.username
        ? `${API_URL}/sites?username=${encodeURIComponent(user.username)}`
        : `${API_URL}/sites`
      const res = await fetch(url)
      if (!res.ok) throw new Error('Failed to fetch sites')
      return res.json()
    },
    enabled: !!user,
    refetchInterval: 30_000,
    staleTime: 25_000,
  })
}
