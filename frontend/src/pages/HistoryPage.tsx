import { useState } from 'react'
import { usePRODSites } from '../hooks/usePRODSites'
import { useHistory } from '../hooks/useHistory'
import { DiffViewer } from '../components/dashboard/DiffViewer'
import { ScoreBadge } from '../components/shared/ScoreBadge'
import { LoadingSpinner } from '../components/shared/LoadingSpinner'
import type { PromptHistoryItem } from '../types'

// ── tiny helpers ──────────────────────────────────────────────────────────────

function Badge({ label, variant }: { label: string; variant: 'prod' | 'qa' | 'dev' | 'fail' | 'pending' | 'pass' }) {
  const cls = {
    prod:    'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30',
    qa:      'bg-amber-500/20  text-amber-300  border border-amber-500/30',
    dev:     'bg-slate-500/20  text-slate-300  border border-slate-500/30',
    fail:    'bg-red-500/20    text-red-300    border border-red-500/30',
    pass:    'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30',
    pending: 'bg-slate-700     text-slate-500  border border-slate-600',
  }[variant]
  return (
    <span className={`inline-block px-2 py-0.5 rounded text-[10px] font-bold tracking-widest uppercase ${cls}`}>
      {label}
    </span>
  )
}

function timeAgo(dateStr: string): string {
  const diff = Date.now() - new Date(dateStr).getTime()
  const mins = Math.floor(diff / 60000)
  if (mins < 1) return 'just now'
  if (mins < 60) return `${mins}m ago`
  const hrs = Math.floor(mins / 60)
  if (hrs < 24) return `${hrs}h ago`
  return `${Math.floor(hrs / 24)}d ago`
}

// ── version card ──────────────────────────────────────────────────────────────

function VersionCard({
  version,
  index,
  total,
  next,
}: {
  version: PromptHistoryItem
  index: number
  total: number
  next: PromptHistoryItem | null
}) {
  const [showDiff, setShowDiff] = useState(false)
  const [showPrompt, setShowPrompt] = useState(false)

  const hash = version.contenthash.slice(0, 7)
  const isNewest = index === 0
  const isProd = !!version.prod_current
  const isQA   = !!version.qa_current
  const isDev  = !!version.dev_current
  const hasFailed  = version.regressionflag === true
  const hasPassed  = version.regressionflag === false && version.goldenscore !== null
  const isPending  = version.goldenscore === null

  const dotColor = isProd   ? 'bg-emerald-400 ring-4 ring-emerald-400/20'
                 : hasFailed ? 'bg-red-400'
                 : hasPassed ? 'bg-emerald-400'
                 : 'bg-slate-600'

  const stageName = version.evalstage === 'DEV_TO_QA' ? 'DEV → QA' : version.evalstage === 'QA_TO_PROD' ? 'QA → PROD' : null

  const systemPreview = version.systemtemplate
    ? version.systemtemplate.length > 180
      ? version.systemtemplate.slice(0, 180) + '…'
      : version.systemtemplate
    : null

  return (
    <div className="relative flex gap-4">
      {/* spine */}
      <div className="flex flex-col items-center shrink-0 pt-1">
        <div className={`w-3 h-3 rounded-full ${dotColor} shrink-0`} />
        {index < total - 1 && <div className="w-px flex-1 bg-slate-700/60 my-1.5 min-h-[24px]" />}
      </div>

      {/* card */}
      <div className="flex-1 mb-4">
        <div className={`rounded-xl border ${isProd ? 'border-emerald-500/30 bg-emerald-500/5' : hasFailed ? 'border-red-500/20 bg-red-500/5' : 'border-slate-700 bg-slate-800/50'} p-4`}>

          {/* top row: hash + badges + "latest" label */}
          <div className="flex items-center flex-wrap gap-2 mb-2">
            <code className="text-xs font-mono text-slate-400 bg-slate-900 px-2 py-0.5 rounded">{hash}</code>
            {isProd && <Badge label="PROD" variant="prod" />}
            {isQA   && <Badge label="QA"   variant="qa" />}
            {isDev  && <Badge label="DEV"  variant="dev" />}
            {hasFailed  && <Badge label="Failed"  variant="fail" />}
            {hasPassed && !isProd && !isQA && !isDev && <Badge label="Passed" variant="pass" />}
            {isPending  && <Badge label="Pending" variant="pending" />}
            {isNewest && (
              <span className="ml-auto text-[10px] text-slate-600 uppercase tracking-widest font-semibold">latest</span>
            )}
          </div>

          {/* commit message */}
          <p className="text-white font-semibold text-sm mb-3 leading-snug">
            {version.commitmessage || '(no commit message)'}
          </p>

          {/* meta row */}
          <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-500 mb-3">
            <span>
              <span className="text-slate-600 mr-1">by</span>
              <span className="text-slate-300">{version.submittedby}</span>
            </span>
            <span title={new Date(version.createdat).toLocaleString()}>
              {timeAgo(version.createdat)} · {new Date(version.createdat).toLocaleDateString()}
            </span>
            {version.prompttype && (
              <span className="text-slate-600">{version.prompttype}</span>
            )}
          </div>

          {/* eval section */}
          {!isPending && (
            <div className={`rounded-lg p-3 mb-3 ${hasFailed ? 'bg-red-950/40 border border-red-900/30' : 'bg-slate-900/60 border border-slate-700'}`}>
              <div className="flex items-center gap-2 mb-2">
                <span className="text-[10px] uppercase tracking-widest font-semibold text-slate-500">
                  Evaluation {stageName ? `· ${stageName}` : ''}
                </span>
                {hasFailed
                  ? <span className="text-red-400 text-xs">✗ Did not pass</span>
                  : <span className="text-emerald-400 text-xs">✓ Passed</span>
                }
              </div>
              <div className="flex gap-4 mb-2">
                <div>
                  <p className="text-[10px] text-slate-600 uppercase tracking-wide mb-0.5">Accuracy</p>
                  <ScoreBadge score={version.goldenscore} type="golden" />
                </div>
                <div>
                  <p className="text-[10px] text-slate-600 uppercase tracking-wide mb-0.5">Judge</p>
                  <ScoreBadge score={version.judgescore} type="judge" />
                </div>
              </div>
            </div>
          )}

          {/* prompt preview */}
          {systemPreview && (
            <div className="mb-3">
              <button
                onClick={() => setShowPrompt(p => !p)}
                className="text-xs text-slate-500 hover:text-slate-300 transition-colors mb-1.5 flex items-center gap-1"
              >
                {showPrompt ? '▲ Hide prompt' : '▼ Show prompt'}
              </button>
              {showPrompt && (
                <pre className="text-xs text-slate-400 bg-slate-900 rounded-lg p-3 border border-slate-700 whitespace-pre-wrap leading-relaxed max-h-48 overflow-y-auto font-sans">
                  {version.systemtemplate}
                </pre>
              )}
              {!showPrompt && (
                <p className="text-xs text-slate-600 italic leading-relaxed">
                  {systemPreview}
                </p>
              )}
            </div>
          )}

          {/* diff toggle */}
          {next && (
            <div>
              <button
                onClick={() => setShowDiff(d => !d)}
                className="text-xs text-indigo-400 hover:text-indigo-300 transition-colors"
              >
                {showDiff ? '▲ Hide diff vs previous' : '▼ Compare with previous version'}
              </button>
              {showDiff && (
                <div className="mt-3 space-y-2">
                  {version.systemtemplate !== next.systemtemplate ? (
                    <DiffViewer
                      before={next.systemtemplate ?? ''}
                      after={version.systemtemplate ?? ''}
                      label="System template changes"
                    />
                  ) : (
                    <p className="text-xs text-slate-600 italic">System template unchanged.</p>
                  )}
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

// ── summary bar ───────────────────────────────────────────────────────────────

function EnvSummaryCard({
  env,
  version,
}: {
  env: 'PROD' | 'QA' | 'DEV'
  version: PromptHistoryItem | undefined
}) {
  const colors = {
    PROD: { label: 'text-emerald-300', border: 'border-emerald-500/20', bg: 'bg-emerald-500/5' },
    QA:   { label: 'text-amber-300',   border: 'border-amber-500/20',   bg: 'bg-amber-500/5'   },
    DEV:  { label: 'text-slate-300',   border: 'border-slate-600',      bg: 'bg-slate-800/40'  },
  }[env]

  return (
    <div className={`flex-1 min-w-[140px] rounded-lg border ${colors.border} ${colors.bg} px-3 py-2.5`}>
      <p className={`text-[10px] font-bold tracking-widest uppercase mb-1.5 ${colors.label}`}>{env}</p>
      {version ? (
        <>
          <code className="text-xs font-mono text-slate-300">{version.contenthash.slice(0, 7)}</code>
          <p className="text-xs text-slate-500 mt-0.5 truncate">{version.commitmessage}</p>
          <p className="text-[10px] text-slate-600 mt-0.5">{timeAgo(version.createdat)}</p>
        </>
      ) : (
        <p className="text-xs text-slate-600 italic">Not yet promoted</p>
      )}
    </div>
  )
}

// ── page ──────────────────────────────────────────────────────────────────────

export function HistoryPage() {
  const { data: sites } = usePRODSites()
  const [selectedSite, setSelectedSite] = useState('')
  const [customSite, setCustomSite] = useState('')
  const [useCustom, setUseCustom] = useState(false)
  const { data: history, isLoading, isError } = useHistory(selectedSite)

  const prodVersion = history?.find(v => v.prod_current)
  const qaVersion   = history?.find(v => v.qa_current)
  const devVersion  = history?.find(v => v.dev_current)

  function loadSite(name: string) {
    if (name.trim()) setSelectedSite(name.trim())
  }

  return (
    <div className="p-6 overflow-y-auto h-full max-w-2xl">
      <h1 className="text-white text-xl font-semibold mb-1">Version History</h1>
      <p className="text-slate-500 text-sm mb-5">
        Browse every version of a site — which environment it's in, evaluation scores, and what changed between versions.
      </p>

      {/* Site picker */}
      <div className="mb-6 space-y-2">
        {!useCustom ? (
          <div className="flex gap-2">
            <select
              className="flex-1 bg-slate-800 border border-slate-600 rounded px-3 py-2 text-sm text-white focus:outline-none focus:border-indigo-500 appearance-none"
              value={selectedSite}
              onChange={e => {
                if (e.target.value === '__custom__') {
                  setUseCustom(true)
                } else {
                  setSelectedSite(e.target.value)
                }
              }}
            >
              <option value="">— Select a site —</option>
              {sites?.map(s => (
                <option key={s.refname} value={s.refname}>{s.refname}</option>
              ))}
              <option value="__custom__">Type a site name…</option>
            </select>
          </div>
        ) : (
          <div className="flex gap-2">
            <input
              autoFocus
              className="flex-1 bg-slate-800 border border-slate-600 rounded px-3 py-2 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500"
              placeholder="Enter site name…"
              value={customSite}
              onChange={e => setCustomSite(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && loadSite(customSite)}
            />
            <button
              onClick={() => loadSite(customSite)}
              disabled={!customSite.trim()}
              className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-30 text-white text-sm font-medium rounded transition-colors"
            >
              Load
            </button>
            <button
              onClick={() => { setUseCustom(false); setCustomSite('') }}
              className="px-3 py-2 text-slate-400 hover:text-white text-sm rounded border border-slate-600 transition-colors"
            >
              ✕
            </button>
          </div>
        )}
      </div>

      {/* Env summary */}
      {selectedSite && history && history.length > 0 && (
        <div className="flex gap-2 mb-6 flex-wrap">
          <EnvSummaryCard env="PROD" version={prodVersion} />
          <EnvSummaryCard env="QA"   version={qaVersion} />
          <EnvSummaryCard env="DEV"  version={devVersion} />
        </div>
      )}

      {/* States */}
      {isLoading && <LoadingSpinner />}

      {isError && (
        <p className="text-red-400 text-sm">Could not load history for "{selectedSite}". Check the site name.</p>
      )}

      {selectedSite && !isLoading && !isError && history?.length === 0 && (
        <p className="text-slate-500 text-sm">No versions found for "{selectedSite}".</p>
      )}

      {/* Timeline */}
      {history && history.length > 0 && (
        <div>
          <p className="text-xs text-slate-600 uppercase tracking-widest font-semibold mb-3">
            {history.length} version{history.length !== 1 ? 's' : ''} · newest first
          </p>
          {history.map((v, i) => (
            <VersionCard
              key={v.contenthash}
              version={v}
              index={i}
              total={history.length}
              next={history[i + 1] ?? null}
            />
          ))}
        </div>
      )}
    </div>
  )
}
