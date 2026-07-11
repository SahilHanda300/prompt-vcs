import { useState } from 'react'
import { useQAFailures, useDegradedCases } from '../../hooks/useEvalResults'
import { ScoreBadge } from '../shared/ScoreBadge'
import { LoadingSpinner } from '../shared/LoadingSpinner'
import type { QAFailureItem, DegradedTestCase } from '../../types'

// ── test case row ─────────────────────────────────────────────────────────────

function TestCaseRow({ tc, index }: { tc: DegradedTestCase; index: number }) {
  const [expanded, setExpanded] = useState(false)
  const isPoor = tc.goldenscore < 0.5

  return (
    <div className={`rounded-lg border ${isPoor ? 'border-red-800/40 bg-red-950/20' : 'border-slate-700 bg-slate-900/40'} overflow-hidden`}>
      {/* header row — always visible */}
      <button
        onClick={() => setExpanded(e => !e)}
        className="w-full flex items-center gap-3 px-3 py-2.5 text-left hover:bg-white/5 transition-colors"
      >
        <span className="text-xs text-slate-500 font-mono w-5 shrink-0">#{index + 1}</span>
        <span className="flex-1 text-sm text-slate-300 truncate">{tc.inputtext}</span>
        <div className="flex items-center gap-2 shrink-0">
          <ScoreBadge score={tc.goldenscore} type="golden" />
          <ScoreBadge score={tc.judgescore} type="judge" />
          <span className="text-slate-600 text-xs">{expanded ? '▲' : '▼'}</span>
        </div>
      </button>

      {/* expanded detail */}
      {expanded && (
        <div className="px-3 pb-3 space-y-2.5 border-t border-slate-700/50 pt-3">
          <div>
            <p className="text-[10px] text-slate-600 uppercase tracking-widest mb-1">Input</p>
            <p className="text-xs text-slate-300 leading-relaxed bg-slate-900 rounded p-2 border border-slate-700">{tc.inputtext}</p>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
            <div>
              <p className="text-[10px] text-emerald-600 uppercase tracking-widest mb-1">Expected</p>
              <p className="text-xs text-slate-300 leading-relaxed bg-emerald-950/30 border border-emerald-900/30 rounded p-2 min-h-[48px]">
                {tc.expectedoutput}
              </p>
            </div>
            <div>
              <p className="text-[10px] text-red-500 uppercase tracking-widest mb-1">Actual (got)</p>
              <p className="text-xs text-slate-300 leading-relaxed bg-red-950/30 border border-red-900/30 rounded p-2 min-h-[48px]">
                {tc.actualoutput}
              </p>
            </div>
          </div>
          {tc.judgereasoning && (
            <div>
              <p className="text-[10px] text-slate-600 uppercase tracking-widest mb-1">Judge reasoning</p>
              <p className="text-xs text-slate-400 italic leading-relaxed">{tc.judgereasoning}</p>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

// ── failure card ──────────────────────────────────────────────────────────────

function FailureCard({ failure }: { failure: QAFailureItem }) {
  const [open, setOpen] = useState(false)
  const { data: cases, isLoading: casesLoading } = useDegradedCases(
    failure.evalid,
    open,
  )

  const stageName = failure.stage === 'DEV_TO_QA' ? 'DEV → QA' : 'QA → PROD'
  const timeAgo = (() => {
    const diff = Date.now() - new Date(failure.runat).getTime()
    const mins = Math.floor(diff / 60000)
    if (mins < 1) return 'just now'
    if (mins < 60) return `${mins}m ago`
    const hrs = Math.floor(mins / 60)
    if (hrs < 24) return `${hrs}h ago`
    return `${Math.floor(hrs / 24)}d ago`
  })()

  return (
    <div className="bg-slate-800/60 border border-red-900/30 rounded-xl overflow-hidden">
      {/* card header — click to expand */}
      <button
        onClick={() => setOpen(o => !o)}
        className="w-full text-left p-4 hover:bg-white/5 transition-colors"
      >
        <div className="flex items-start justify-between gap-3">
          <div className="flex-1 min-w-0">
            <div className="flex items-center flex-wrap gap-2 mb-1.5">
              <span className="text-white font-semibold">{failure.refname ?? 'unknown'}</span>
              <span className="px-2 py-0.5 rounded text-[10px] font-bold tracking-widest uppercase bg-red-500/20 text-red-300 border border-red-500/30">
                FAILED {stageName}
              </span>
            </div>
            <p className="text-xs text-slate-500 truncate">{failure.commitmessage}</p>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            <ScoreBadge score={failure.goldenscore} type="golden" />
            <ScoreBadge score={failure.judgescore} type="judge" />
            <span className="text-slate-600 text-sm ml-1">{open ? '▲' : '▼'}</span>
          </div>
        </div>

        <div className="flex items-center gap-3 mt-2 text-xs text-slate-600">
          <span>{failure.submittedby}</span>
          <span>·</span>
          <span title={new Date(failure.runat).toLocaleString()}>{timeAgo}</span>
          <span>·</span>
          <span>{new Date(failure.runat).toLocaleDateString()}</span>
        </div>
      </button>

      {/* expanded content */}
      {open && (
        <div className="border-t border-slate-700 px-4 py-4 space-y-4">
          {/* reason box */}
          <div className="bg-slate-900/60 rounded-lg border border-slate-700 p-3">
            <p className="text-[10px] text-slate-600 uppercase tracking-widest mb-1.5">Why it failed</p>
            <p className="text-sm text-slate-300 leading-relaxed">{failure.promotionreason ?? 'No reason recorded.'}</p>
          </div>

          {/* test cases */}
          <div>
            <p className="text-[10px] text-slate-600 uppercase tracking-widest mb-2">
              Failed test cases
              {cases && ` (${cases.length})`}
            </p>

            {casesLoading && (
              <div className="flex items-center gap-2 py-3">
                <div className="w-3 h-3 rounded-full bg-slate-600 animate-pulse" />
                <span className="text-xs text-slate-500">Loading test cases…</span>
              </div>
            )}

            {cases && cases.length === 0 && (
              <p className="text-xs text-slate-600 italic">No individual test case records stored.</p>
            )}

            {cases && cases.length > 0 && (
              <div className="space-y-2">
                {cases.map((tc, i) => (
                  <TestCaseRow key={tc.resultid} tc={tc} index={i} />
                ))}
              </div>
            )}
          </div>

          {/* hash */}
          <p className="text-[10px] text-slate-700 font-mono">
            eval {failure.evalid} · prompt {failure.prompthash.slice(0, 12)}…
          </p>
        </div>
      )}
    </div>
  )
}

// ── panel ─────────────────────────────────────────────────────────────────────

export function QAFailuresPanel() {
  const { data, isLoading, error } = useQAFailures()
  const [filter, setFilter] = useState('')

  if (isLoading) return <LoadingSpinner />
  if (error) return <p className="text-red-400 text-sm">Failed to load failures.</p>
  if (!data?.length) return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      <div className="text-3xl mb-3">✓</div>
      <p className="text-slate-300 font-medium">No QA failures</p>
      <p className="text-slate-500 text-sm mt-1">Every submitted prompt has passed evaluation.</p>
    </div>
  )

  const filtered = filter.trim()
    ? data.filter(f => (f.refname ?? '').toLowerCase().includes(filter.toLowerCase()))
    : data

  return (
    <div className="space-y-4">
      {/* filter */}
      <div className="flex items-center gap-3">
        <input
          className="bg-slate-800 border border-slate-600 rounded px-3 py-1.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 w-48"
          placeholder="Filter by site…"
          value={filter}
          onChange={e => setFilter(e.target.value)}
        />
        <span className="text-xs text-slate-600">
          {filtered.length} of {data.length} failure{data.length !== 1 ? 's' : ''}
        </span>
      </div>

      {filtered.length === 0 && (
        <p className="text-slate-500 text-sm">No failures match "{filter}".</p>
      )}

      {filtered.map(f => (
        <FailureCard key={f.evalid} failure={f} />
      ))}
    </div>
  )
}
