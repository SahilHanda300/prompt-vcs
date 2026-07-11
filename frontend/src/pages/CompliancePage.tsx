import { useState } from 'react'
import { usePRODSites } from '../hooks/usePRODSites'
import { CompliancePanel } from '../components/dashboard/CompliancePanel'

export function CompliancePage() {
  const { data: sites } = usePRODSites()
  const [filter, setFilter] = useState('')

  return (
    <div className="p-6 overflow-y-auto h-full space-y-4">
      <div>
        <h1 className="text-white text-xl font-semibold">Compliance Audit Trail</h1>
        <p className="text-slate-400 text-sm mt-1">
          EU AI Act Article 12 — every automated promotion decision with statistical evidence.
        </p>
      </div>

      <select
        className="bg-slate-800 border border-slate-600 rounded px-3 py-2 text-sm text-white focus:outline-none focus:border-indigo-500"
        value={filter}
        onChange={e => setFilter(e.target.value)}
      >
        <option value="">All sites</option>
        {sites?.map(s => <option key={s.refname} value={s.refname}>{s.refname}</option>)}
      </select>

      <CompliancePanel refName={filter || undefined} />
    </div>
  )
}
