import { usePRODDashboard } from '../../hooks/useEvalResults'
import { ScoreBadge } from '../shared/ScoreBadge'
import { VersionTag } from '../shared/VersionTag'
import { LoadingSpinner } from '../shared/LoadingSpinner'
import { Link } from 'react-router-dom'

export function PRODSitesPanel() {
  const { data, isLoading, error } = usePRODDashboard()

  if (isLoading) return <LoadingSpinner />
  if (error) return <p className="text-red-400 text-sm">Failed to load dashboard.</p>
  if (!data?.length) return <p className="text-slate-500 text-sm">No PROD sites yet.</p>

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-slate-700 text-slate-400 text-left">
            <th className="pb-2 pr-4 font-medium">Site</th>
            <th className="pb-2 pr-4 font-medium">Type</th>
            <th className="pb-2 pr-4 font-medium">Golden</th>
            <th className="pb-2 pr-4 font-medium">Judge</th>
            <th className="pb-2 pr-4 font-medium">Version</th>
            <th className="pb-2 font-medium">Submitted by</th>
          </tr>
        </thead>
        <tbody>
          {data.map(site => (
            <tr key={site.refname} className="border-b border-slate-800 hover:bg-slate-800/50">
              <td className="py-2 pr-4">
                <Link to={`/sites/${site.refname}`} className="text-indigo-400 hover:underline font-medium">
                  {site.refname}
                </Link>
              </td>
              <td className="py-2 pr-4 text-slate-400">{site.prompttype}</td>
              <td className="py-2 pr-4"><ScoreBadge score={site.goldenscore} type="golden" /></td>
              <td className="py-2 pr-4"><ScoreBadge score={site.judgescore} type="judge" /></td>
              <td className="py-2 pr-4"><VersionTag hash={site.targethash} /></td>
              <td className="py-2 text-slate-400">{site.submittedby}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
