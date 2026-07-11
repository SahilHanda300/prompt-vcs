import { useQuery } from '@tanstack/react-query'
import { API_URL } from '../lib/api'
import type { PromotionAuditItem } from '../types'

export function useCompliance(refName?: string) {
  return useQuery<PromotionAuditItem[]>({
    queryKey: ['compliance', refName],
    queryFn: async () => {
      const url = refName
        ? `${API_URL}/compliance/audit-trail?ref_name=${encodeURIComponent(refName)}`
        : `${API_URL}/compliance/audit-trail`
      const res = await fetch(url)
      if (!res.ok) throw new Error('Failed to fetch audit trail')
      return res.json()
    },
  })
}
