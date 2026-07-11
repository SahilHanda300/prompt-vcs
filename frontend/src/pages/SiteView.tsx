import { useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { API_URL } from '../lib/api'
import { LoadingSpinner } from '../components/shared/LoadingSpinner'
import { ChatInterface } from '../components/interfaces/ChatInterface'
import { TransformInterface } from '../components/interfaces/TransformInterface'
import { AnalyseInterface } from '../components/interfaces/AnalyseInterface'
import { GenerateInterface } from '../components/interfaces/GenerateInterface'
import { QAInterface } from '../components/interfaces/QAInterface'
import { GeneratedUIInterface } from '../components/interfaces/GeneratedUIInterface'
import type { SiteListItem } from '../types'

export function SiteView() {
  const { name } = useParams<{ name: string }>()

  const { data: site, isLoading, error } = useQuery<SiteListItem>({
    queryKey: ['site', name],
    queryFn: async () => {
      const res = await fetch(`${API_URL}/sites`)
      if (!res.ok) throw new Error('Failed to fetch sites')
      const sites: SiteListItem[] = await res.json()
      const found = sites.find(s => s.refname === name)
      if (!found) throw new Error(`Site '${name}' not found`)
      return found
    },
    enabled: !!name,
  })

  if (isLoading) return <LoadingSpinner />
  if (error || !site) return <p className="p-8 text-red-400">Site not found.</p>

  const type = site.prompttype

  if (type === 'generated-ui') return <GeneratedUIInterface site={site} />
  if (type === 'transform') return <TransformInterface site={site} />
  if (type === 'analyse') return <AnalyseInterface site={site} />
  if (type === 'generate') return <GenerateInterface site={site} />
  if (type === 'qa') return <QAInterface site={site} />
  return <ChatInterface site={site} />
}
