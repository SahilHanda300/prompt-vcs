import { useQuery } from '@tanstack/react-query'
import { API_URL } from '../lib/api'
import type { PromptHistoryItem } from '../types'

export function useHistory(refName: string) {
  return useQuery<PromptHistoryItem[]>({
    queryKey: ['history', refName],
    queryFn: async () => {
      const res = await fetch(`${API_URL}/prompts/${encodeURIComponent(refName)}/history/identity`)
      if (!res.ok) throw new Error('Failed to fetch history')
      return res.json()
    },
    enabled: refName.length > 0,
  })
}
