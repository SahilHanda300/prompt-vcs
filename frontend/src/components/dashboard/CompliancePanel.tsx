import { useState } from 'react'
import { useCompliance } from '../../hooks/useCompliance'
import { ScoreBadge } from '../shared/ScoreBadge'
import { LoadingSpinner } from '../shared/LoadingSpinner'
import type { PromotionAuditItem } from '../../types'

interface CompliancePanelProps {
  refName?: string
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

function parseReason(reason: string | null) {
  if (!reason) return null
  const pMatch = reason.match(/p=([\d.]+)/)
  const dMatch = reason.match(/d=(-?[\d.]+)/)
  const goldenMatch = reason.match(/GoldenScore=([\d.]+)/)
  const judgeMatch = reason.match(/JudgeScore=([\d./]+)/)
  return {
    p: pMatch ? pMatch[1] : null,
    d: dMatch ? dMatch[1] : null,
    golden: goldenMatch ? goldenMatch[1] : null,
    judge: judgeMatch ? judgeMatch[1] : null,
  }
}

function AuditCard({ item }: { item: PromotionAuditItem }) {
  const [open, setOpen] = useState(false)
  const passed = !item.regressionflag
  const stageName = item.stage === 'DEV_TO_QA' ? 'DEV → QA' : item.stage === 'QA_TO_PROD' ? 'QA → PROD' : item.stage
  const parsed = parseReason(item.promotionreason)

  return (
    <div className={`rounded-xl border overflow-hidden ${
      passed
        ? 'border-emerald-200 bg-emerald-50 dark:border-emerald-900/40 dark:bg-emerald-500/5'
        : 'border-red-200 bg-red-50 dark:border-red-900/40 dark:bg-red-500/5'
    }`}>
      <button
        onClick={() => setOpen(o => !o)}
        className="w-full text-left p-4 hover:bg-black/5 dark:hover:bg-white/5 transition-colors"
      >
        <div className="flex items-start justify-between gap-3">
          <div className="flex-1 min-w-0">
            <div className="flex items-center flex-wrap gap-2 mb-1.5">
              <span className="text-gray-900 dark:text-white font-semibold">{item.promptname ?? '—'}</span>
              <span className={`px-2 py-0.5 rounded text-[10px] font-bold tracking-widest uppercase border ${
                passed
                  ? 'bg-emerald-100 text-emerald-700 border-emerald-200 dark:bg-emerald-500/20 dark:text-emerald-300 dark:border-emerald-500/30'
                  : 'bg-red-100 text-red-700 border-red-200 dark:bg-red-500/20 dark:text-red-300 dark:border-red-500/30'
              }`}>
                {stageName}
              </span>
              <span className={`text-xs font-medium ${passed ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400'}`}>
                {passed ? '✓ Passed' : '✗ Failed'}
              </span>
            </div>
            <p className="text-xs text-gray-400 dark:text-slate-500 truncate">
              {item.commitmessage}
            </p>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            <ScoreBadge score={item.goldenscore} type="golden" />
            <ScoreBadge score={item.judgescore} type="judge" />
            <span className="text-gray-400 dark:text-slate-600 text-sm ml-1">{open ? '▲' : '▼'}</span>
          </div>
        </div>

        <div className="flex flex-wrap gap-3 mt-2 text-xs text-gray-400 dark:text-slate-600">
          <span>{item.submittedby}</span>
          <span>·</span>
          <span title={new Date(item.runat).toLocaleString()}>{timeAgo(item.runat)}</span>
          <span>·</span>
          <span>{new Date(item.runat).toLocaleDateString()}</span>
        </div>
      </button>

      {open && (
        <div className="border-t border-gray-200 dark:border-slate-700/50 px-4 py-4 space-y-4">

          {parsed && (
            <div>
              <p className="text-[10px] text-gray-400 dark:text-slate-600 uppercase tracking-widest mb-2">Statistical Evidence</p>
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
                <StatBox label="Accuracy" value={parsed.golden ?? '—'} highlight={false} />
                <StatBox label="Judge Score" value={parsed.judge ? `${parsed.judge}/5` : '—'} highlight={false} />
                <StatBox
                  label="p-value"
                  value={parsed.p ?? '—'}
                  highlight={!!parsed.p}
                  good={parsed.p ? parseFloat(parsed.p) < 0.05 : false}
                  threshold="< 0.05"
                />
                <StatBox
                  label="Cohen's d"
                  value={parsed.d ?? '—'}
                  highlight={!!parsed.d}
                  good={parsed.d ? Math.abs(parseFloat(parsed.d)) >= 0.5 : false}
                  threshold="≥ 0.5"
                />
              </div>
            </div>
          )}

          <div>
            <p className="text-[10px] text-gray-400 dark:text-slate-600 uppercase tracking-widest mb-2">
              Decision Record
              <span className="ml-2 normal-case text-gray-300 dark:text-slate-700">· EU AI Act Article 12</span>
            </p>
            <pre className="text-xs text-gray-700 dark:text-slate-300 bg-white dark:bg-slate-900 border border-gray-200 dark:border-slate-700 rounded-lg p-3 whitespace-pre-wrap leading-relaxed font-sans">
              {item.promotionreason ?? 'No reason recorded.'}
            </pre>
          </div>

          <div className={`rounded-lg p-3 text-xs leading-relaxed ${
            passed
              ? 'bg-emerald-50 border border-emerald-200 text-emerald-800 dark:bg-emerald-950/40 dark:border-emerald-900/30 dark:text-emerald-200'
              : 'bg-red-50 border border-red-200 text-red-800 dark:bg-red-950/40 dark:border-red-900/30 dark:text-red-200'
          }`}>
            {passed ? (
              <>
                <span className="font-semibold">Gate passed.</span> Regression was not detected —
                either p ≥ 0.05 or Cohen's d &lt; 0.5 (both conditions must be met to block promotion).
                This prompt was automatically promoted.
              </>
            ) : (
              <>
                <span className="font-semibold">Gate blocked.</span> Both p &lt; 0.05 and Cohen's d ≥ 0.5
                were met, indicating a statistically significant regression. Promotion was automatically
                blocked and the prompt remained in its current environment.
              </>
            )}
          </div>
        </div>
      )}
    </div>
  )
}

function StatBox({
  label, value, highlight, good, threshold,
}: {
  label: string
  value: string
  highlight: boolean
  good?: boolean
  threshold?: string
}) {
  return (
    <div className="bg-white dark:bg-slate-900 border border-gray-200 dark:border-slate-700 rounded-lg px-3 py-2.5 text-center">
      <p className="text-[10px] text-gray-400 dark:text-slate-600 uppercase tracking-widest mb-1">{label}</p>
      <p className={`text-sm font-mono font-semibold ${
        highlight
          ? good
            ? 'text-emerald-600 dark:text-emerald-300'
            : 'text-red-600 dark:text-red-300'
          : 'text-gray-700 dark:text-slate-300'
      }`}>
        {value}
      </p>
      {threshold && (
        <p className="text-[9px] text-gray-300 dark:text-slate-700 mt-0.5">{threshold}</p>
      )}
    </div>
  )
}

export function CompliancePanel({ refName }: CompliancePanelProps) {
  const { data, isLoading, error } = useCompliance(refName)

  if (isLoading) return <LoadingSpinner />
  if (error) return <p className="text-red-500 dark:text-red-400 text-sm">Failed to load audit trail.</p>
  if (!data?.length) return <p className="text-gray-400 dark:text-slate-500 text-sm">No promotion decisions recorded.</p>

  const passed = data.filter(d => !d.regressionflag).length
  const failed = data.filter(d => d.regressionflag).length

  return (
    <div className="space-y-4">
      <div className="flex gap-3 flex-wrap">
        <div className="bg-white dark:bg-slate-800 border border-gray-200 dark:border-slate-700 rounded-lg px-4 py-2 text-center">
          <p className="text-[10px] text-gray-400 dark:text-slate-600 uppercase tracking-widest">Total decisions</p>
          <p className="text-lg font-semibold text-gray-900 dark:text-white">{data.length}</p>
        </div>
        <div className="bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-900/30 rounded-lg px-4 py-2 text-center">
          <p className="text-[10px] text-emerald-600 dark:text-emerald-700 uppercase tracking-widest">Passed</p>
          <p className="text-lg font-semibold text-emerald-700 dark:text-emerald-300">{passed}</p>
        </div>
        <div className="bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900/30 rounded-lg px-4 py-2 text-center">
          <p className="text-[10px] text-red-500 dark:text-red-700 uppercase tracking-widest">Blocked</p>
          <p className="text-lg font-semibold text-red-600 dark:text-red-300">{failed}</p>
        </div>
      </div>

      {data.map((item, i) => (
        <AuditCard key={i} item={item} />
      ))}
    </div>
  )
}
