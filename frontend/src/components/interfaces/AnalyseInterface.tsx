import { TransformInterface } from './TransformInterface'
import type { SiteListItem } from '../../types'

export function AnalyseInterface({ site }: { site: SiteListItem }) {
  return <TransformInterface site={site} />
}
