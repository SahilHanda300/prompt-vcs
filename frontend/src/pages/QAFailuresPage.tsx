import { QAFailuresPanel } from '../components/dashboard/QAFailuresPanel'

export function QAFailuresPage() {
  return (
    <div className="p-6 overflow-y-auto h-full">
      <h1 className="text-white text-xl font-semibold mb-4">QA Failures</h1>
      <QAFailuresPanel />
    </div>
  )
}
