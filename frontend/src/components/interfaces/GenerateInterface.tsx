import { TransformInterface } from './TransformInterface'
import type { SiteListItem } from '../../types'

export function GenerateInterface({ site }: { site: SiteListItem }) {
  return <TransformInterface site={site} />
}
