import { PRODSitesPanel } from '../components/dashboard/PRODSitesPanel'
import { QAFailuresPanel } from '../components/dashboard/QAFailuresPanel'

export function DashboardPage() {
  return (
    <div className="p-6 space-y-8 overflow-y-auto h-full">
      <div>
        <h1 className="text-white text-xl font-semibold mb-4">PROD Sites</h1>
        <PRODSitesPanel />
      </div>
      <div>
        <h2 className="text-white text-lg font-semibold mb-4">Recent QA Failures</h2>
        <QAFailuresPanel />
      </div>
    </div>
  )
}
