import { useState, useEffect, useRef } from 'react'
import { API_URL } from '../lib/api'
import { useAuth } from '../lib/AuthContext'

type PipelineStage = 'idle' | 'submitted' | 'dev_running' | 'dev_failed' | 'qa_running' | 'prod_failed' | 'live'

interface PipelineState {
  stage: PipelineStage
  refName: string
  reason?: string
}

export function SubmitPage() {
  const { user } = useAuth()
  const [mode, setMode] = useState<'chat' | 'ui'>('chat')
  const [refName, setRefName] = useState('')
  const [prompt, setPrompt] = useState('')
  const [commitMessage, setCommitMessage] = useState('')
  const submittedBy = user?.username ?? ''
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [pipeline, setPipeline] = useState<PipelineState>({ stage: 'idle', refName: '' })
  const [elapsed, setElapsed] = useState(0)
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const pollCountRef = useRef(0)

  useEffect(() => () => {
    if (pollRef.current) clearInterval(pollRef.current)
    if (timerRef.current) clearInterval(timerRef.current)
  }, [])

  function startPolling(ref: string, contentHash: string, resetTimer = true) {
    if (pollRef.current) clearInterval(pollRef.current)
    pollCountRef.current = 0

    if (resetTimer) {
      if (timerRef.current) clearInterval(timerRef.current)
      setElapsed(0)
      timerRef.current = setInterval(() => setElapsed(s => s + 1), 1000)
    }

    pollRef.current = setInterval(async () => {
      pollCountRef.current += 1

      if (pollCountRef.current > 36) {
        clearInterval(pollRef.current!)
        clearInterval(timerRef.current!)
        setPipeline({ stage: 'dev_failed', refName: ref, reason: 'Pipeline timed out. Check GitHub Actions for details.' })
        return
      }

      try {
        const res = await fetch(`${API_URL}/prompts/eval-status/${contentHash}`)
        if (!res.ok) return
        const data: { status: string; stage: string; reason: string } = await res.json()

        if (data.status === 'pending') return

        clearInterval(pollRef.current!)

        if (data.status === 'failed') {
          clearInterval(timerRef.current!)
          const stage = data.stage === 'QA_TO_PROD' ? 'prod_failed' : 'dev_failed'
          setPipeline({ stage, refName: ref, reason: data.reason })
        } else {
          if (data.stage === 'DEV_TO_QA') {
            setPipeline({ stage: 'qa_running', refName: ref })
            startPolling(ref, contentHash, false)
          } else {
            clearInterval(timerRef.current!)
            setPipeline({ stage: 'live', refName: ref, reason: data.reason })
          }
        }
      } catch { /* ignore */ }
    }, 10_000)
  }

  async function handleSubmit() {
    if (!refName.trim() || !prompt.trim() || !commitMessage.trim() || !submittedBy.trim()) return
    setLoading(true)
    setError('')
    if (pollRef.current) clearInterval(pollRef.current)

    try {
      let parentHash: string | null = null
      try {
        const devRes = await fetch(`${API_URL}/prompts/${encodeURIComponent(refName.trim())}/DEV`)
        if (devRes.ok) {
          const devData = await devRes.json()
          parentHash = devData.contenthash ?? null
        }
      } catch { /* new site — no parent */ }

      const res = await fetch(`${API_URL}/prompts/submit`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ref_name: refName.trim(),
          system_template: prompt.trim(),
          user_template: '{input}',
          model_params: { max_tokens: 512, temperature: 0.7 },
          submitted_by: submittedBy.trim(),
          country: 'Unknown',
          commit_message: commitMessage.trim(),
          prompt_type: mode === 'ui' ? 'generated-ui' : 'chat',
          parent_hash: parentHash,
        }),
      })
      const data = await res.json()
      if (!res.ok) { setError(data.detail ?? 'Submission failed.'); return }

      const ref = refName.trim()
      const contentHash: string = data.content_hash
      setRefName(''); setPrompt(''); setCommitMessage('')
      setPipeline({ stage: 'submitted', refName: ref })
      setTimeout(() => {
        setPipeline({ stage: 'dev_running', refName: ref })
        startPolling(ref, contentHash)
      }, 3000)
    } finally {
      setLoading(false)
    }
  }

  function runningHint(): string {
    if (elapsed < 30) return 'Starting GitHub Actions runner…'
    if (elapsed < 90) return 'Installing evaluation dependencies…'
    if (elapsed < 150) return 'Running golden dataset tests…'
    return 'Running LLM judge scoring…'
  }

  const steps: { key: PipelineStage[]; label: string }[] = [
    { key: ['submitted', 'dev_running', 'dev_failed', 'qa_running', 'prod_failed', 'live'], label: 'Submitted' },
    { key: ['dev_running'], label: 'DEV → QA evaluation running' },
    { key: ['dev_failed'], label: 'DEV → QA failed' },
    { key: ['qa_running', 'prod_failed', 'live'], label: 'Promoted to QA' },
    { key: ['qa_running'], label: 'QA → PROD check running' },
    { key: ['prod_failed'], label: 'QA → PROD failed' },
    { key: ['live'], label: `Site ${pipeline.refName} is live` },
  ]

  const activeSteps = steps.filter(s => s.key.includes(pipeline.stage))

  return (
    <div className="p-8 max-w-xl h-full overflow-y-auto">
      <h1 className="text-gray-900 dark:text-white font-semibold text-lg mb-6">Submit Prompt</h1>

      <div className="flex gap-1 p-1 bg-gray-100 dark:bg-white/5 rounded mb-6">
        {(['chat', 'ui'] as const).map(m => (
          <button
            key={m}
            onClick={() => { setMode(m); setPrompt('') }}
            className={`flex-1 py-1.5 text-xs font-medium rounded transition-colors ${
              mode === m ? 'bg-indigo-600 text-white' : 'text-gray-500 dark:text-slate-400 hover:text-gray-900 dark:hover:text-white'
            }`}
          >
            {m === 'chat' ? 'Chat Assistant' : 'Generate UI'}
          </button>
        ))}
      </div>

      <div className="space-y-4">
        <Field label="Site Name">
          <input
            className="input"
            placeholder={mode === 'ui' ? 'e.g. my-calculator' : 'e.g. customer-bot'}
            value={refName}
            onChange={e => setRefName(e.target.value)}
          />
        </Field>

        <Field label={mode === 'ui' ? 'Describe What to Build' : 'The Prompt'}>
          <textarea
            rows={7}
            className="input resize-none"
            placeholder={
              mode === 'ui'
                ? 'e.g. Create a calculator with basic arithmetic operations'
                : 'You are a helpful assistant that…'
            }
            value={prompt}
            onChange={e => setPrompt(e.target.value)}
          />
        </Field>

        <Field label="What changed?">
          <input
            className="input"
            placeholder="e.g. Initial version / Added stricter tone / Fixed hallucination"
            value={commitMessage}
            onChange={e => setCommitMessage(e.target.value)}
          />
        </Field>

        <Field label="Submitting as">
          <input
            className="input opacity-70 cursor-not-allowed"
            value={submittedBy}
            readOnly
          />
        </Field>

        {error && <p className="text-red-500 dark:text-red-400 text-sm">{error}</p>}

        <button
          onClick={handleSubmit}
          disabled={loading || !refName.trim() || !prompt.trim() || !commitMessage.trim() || !submittedBy.trim()}
          className="w-full py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-30 text-white text-sm font-medium rounded transition-colors"
        >
          {loading ? 'Submitting…' : 'Trigger Pipeline'}
        </button>
      </div>

      {pipeline.stage !== 'idle' && (
        <div className="mt-8 border-t border-gray-200 dark:border-white/5 pt-6 space-y-3">
          {activeSteps.map((s, i) => {
            const isFailed  = s.label.includes('failed')
            const isRunning = s.label.includes('running')
            const isLive    = s.label.includes('live')
            return (
              <div key={i} className="flex items-start gap-3">
                <div className={`mt-1.5 w-1.5 h-1.5 rounded-full shrink-0 ${
                  isFailed ? 'bg-red-500' : isRunning ? 'bg-indigo-500 animate-pulse' : 'bg-emerald-500'
                }`} />
                <div>
                  <p className={`text-sm ${
                    isFailed ? 'text-red-600 dark:text-red-300'
                    : isLive ? 'text-emerald-600 dark:text-emerald-300 font-medium'
                    : 'text-gray-700 dark:text-slate-300'
                  }`}>
                    {s.label}
                    {isRunning && elapsed > 0 && (
                      <span className="ml-2 text-xs text-gray-400 dark:text-slate-600">{elapsed}s</span>
                    )}
                  </p>
                  {isRunning && (
                    <p className="mt-0.5 text-xs text-gray-400 dark:text-slate-600">{runningHint()}</p>
                  )}
                  {isFailed && pipeline.reason && (
                    <p className="mt-1 text-xs text-gray-500 dark:text-slate-500 leading-relaxed">{pipeline.reason}</p>
                  )}
                </div>
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="block text-xs text-gray-500 dark:text-slate-500 mb-1.5 uppercase tracking-wider">{label}</label>
      {children}
    </div>
  )
}
