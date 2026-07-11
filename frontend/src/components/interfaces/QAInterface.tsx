import { TransformInterface } from './TransformInterface'
import type { SiteListItem } from '../../types'

export function QAInterface({ site }: { site: SiteListItem }) {
  return <TransformInterface site={site} />
}
