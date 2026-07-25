import { useState, useEffect, useRef } from 'react'
import { API_URL } from '../lib/api'
import { useAuth } from '../lib/AuthContext'

const COMPLETION_DEBOUNCE_MS = 500
// Commit-summary and quality-score aren't something the user is actively
// waiting on the way ghost-text is — they update quietly in the background.
// Debouncing them longer means they don't fire (and compete for backend/LLM
// capacity) on every single typing pause, which was queuing up behind the
// ghost-text request and making suggestions feel slow to appear.
const BACKGROUND_DEBOUNCE_MS = 1500
// Below this length a draft is too thin to bother scoring — avoids firing on
// "you are a" etc.
const PROMPT_QUALITY_MIN_LENGTH = 20
// Each accepted suggestion feeds a longer prompt into the next completion
// call, and with nothing new from the user to ground it, chained accepts
// drift further off-topic the longer the chain runs. Cap it, and require a
// manual keystroke to reset the count before offering more.
const MAX_CONSECUTIVE_GHOST_ACCEPTS = 3

type PipelineStage = 'idle' | 'submitted' | 'dev_running' | 'dev_failed' | 'qa_running' | 'prod_failed' | 'live'

interface PipelineState {
  stage: PipelineStage
  refName: string
  reason?: string
}

function joinWithSpace(value: string, suffix: string): string {
  const needsSpace = value.length > 0 && !/\s$/.test(value) && !/^[\s.,!?;:]/.test(suffix)
  return value + (needsSpace ? ' ' : '') + suffix
}

// Shared inline "ghost text" completion — an LLM call, debounced as the user
// types, with a cap on how many suggestions can be chained via Tab in a row
// without any new manually-typed input (see MAX_CONSECUTIVE_GHOST_ACCEPTS).
function useGhostCompletion(params: {
  value: string
  enabled: boolean
  context: string
  related?: string
  join: (value: string, suffix: string) => string
}) {
  const { value, enabled, context, related, join } = params
  const [suffix, setSuffix] = useState('')
  const [consecutiveAccepts, setConsecutiveAccepts] = useState(0)
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const abortRef = useRef<AbortController | null>(null)
  const requestIdRef = useRef(0)
  // Read via ref (not a dependency) so `related` arriving later from its own
  // separate, independently-debounced request doesn't reset this effect and
  // chain a second round trip after the first — it just rides along on
  // whichever request the typing debounce below was already going to fire.
  const relatedRef = useRef(related)
  relatedRef.current = related

  useEffect(() => {
    setSuffix('')
    requestIdRef.current += 1
    if (timeoutRef.current) clearTimeout(timeoutRef.current)
    if (abortRef.current) abortRef.current.abort()

    if (!enabled || !value.trim() || consecutiveAccepts >= MAX_CONSECUTIVE_GHOST_ACCEPTS) return

    const requestId = requestIdRef.current
    const text = value

    timeoutRef.current = setTimeout(async () => {
      const controller = new AbortController()
      abortRef.current = controller
      try {
        const res = await fetch(`${API_URL}/complete`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ text, context, related: relatedRef.current }),
          signal: controller.signal,
        })
        if (!res.ok) {
          console.warn(`[ghost-completion] request failed: ${res.status} ${res.statusText}`, await res.text().catch(() => ''))
          return
        }
        const data: { completion: string } = await res.json()
        console.warn('[ghost-completion] response', JSON.stringify(data.completion))
        if (requestId === requestIdRef.current && data.completion) {
          setSuffix(data.completion)
        }
      } catch (err) {
        if ((err as Error)?.name !== 'AbortError') {
          console.warn('[ghost-completion] request errored', err)
        }
      }
    }, COMPLETION_DEBOUNCE_MS)

    return () => {
      if (timeoutRef.current) clearTimeout(timeoutRef.current)
    }
  }, [enabled, value, context, consecutiveAccepts])

  function reset() {
    setConsecutiveAccepts(0)
  }

  function accept(): string | null {
    if (!suffix) return null
    const next = join(value, suffix)
    setSuffix('')
    setConsecutiveAccepts(n => n + 1)
    return next
  }

  return { suffix, accept, reset, capped: consecutiveAccepts >= MAX_CONSECUTIVE_GHOST_ACCEPTS }
}

// Derives a short "what changed" label from the live prompt text via the
// same debounced /complete endpoint — always tracks the current prompt, so
// there's no separate state for the user to keep in sync manually.
function useCommitSummary(text: string): string {
  const [summary, setSummary] = useState('')
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const abortRef = useRef<AbortController | null>(null)
  const requestIdRef = useRef(0)

  useEffect(() => {
    requestIdRef.current += 1
    if (timeoutRef.current) clearTimeout(timeoutRef.current)
    if (abortRef.current) abortRef.current.abort()

    const trimmed = text.trim()
    if (!trimmed) { setSummary(''); return }

    const requestId = requestIdRef.current

    timeoutRef.current = setTimeout(async () => {
      const controller = new AbortController()
      abortRef.current = controller
      try {
        const res = await fetch(`${API_URL}/complete`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ text: trimmed, context: 'commit-summary' }),
          signal: controller.signal,
        })
        if (!res.ok) return
        const data: { completion: string } = await res.json()
        // A blank result here is almost always a transient hiccup (rate
        // limit, model returned nothing), not a real "no summary" state —
        // and since this only re-fires when the prompt text itself changes,
        // applying it would wipe out a good summary with no way to recover
        // until the user edits the prompt again. Keep the last good value.
        if (requestId === requestIdRef.current && data.completion) setSummary(data.completion)
      } catch { /* aborted or network error — leave the last summary in place */ }
    }, BACKGROUND_DEBOUNCE_MS)

    return () => {
      if (timeoutRef.current) clearTimeout(timeoutRef.current)
    }
  }, [text])

  return summary
}

interface PromptQuality {
  score: number
  feedback: string
}

// Live "is this prompt good enough?" indicator — an AI-estimated confidence
// score (not a guaranteed pass predictor), so it works from the very first
// draft of a brand-new site, before any test dataset exists. Uses the same
// debounced /complete endpoint as the other inline-assist features.
function usePromptQuality(text: string): PromptQuality | null {
  const [quality, setQuality] = useState<PromptQuality | null>(null)
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const abortRef = useRef<AbortController | null>(null)
  const requestIdRef = useRef(0)

  useEffect(() => {
    requestIdRef.current += 1
    if (timeoutRef.current) clearTimeout(timeoutRef.current)
    if (abortRef.current) abortRef.current.abort()

    const trimmed = text.trim()
    if (trimmed.length < PROMPT_QUALITY_MIN_LENGTH) { setQuality(null); return }

    const requestId = requestIdRef.current

    timeoutRef.current = setTimeout(async () => {
      const controller = new AbortController()
      abortRef.current = controller
      try {
        const res = await fetch(`${API_URL}/complete`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ text: trimmed, context: 'prompt-quality' }),
          signal: controller.signal,
        })
        if (!res.ok) return
        const data: { completion: string } = await res.json()
        const [scoreRaw, ...rest] = data.completion.split('|')
        const score = Number.parseInt(scoreRaw, 10)
        if (requestId === requestIdRef.current && !Number.isNaN(score)) {
          setQuality({ score: Math.min(Math.max(score, 0), 100), feedback: rest.join('|').trim() })
        }
      } catch { /* aborted or network error — leave the last score in place */ }
    }, BACKGROUND_DEBOUNCE_MS)

    return () => {
      if (timeoutRef.current) clearTimeout(timeoutRef.current)
    }
  }, [text])

  return quality
}

export function SubmitPage() {
  const { user } = useAuth()
  const [mode, setMode] = useState<'chat' | 'ui'>('chat')
  const [refName, setRefName] = useState('')
  const [prompt, setPrompt] = useState('')
  const submittedBy = user?.username ?? ''
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [pipeline, setPipeline] = useState<PipelineState>({ stage: 'idle', refName: '' })
  const [elapsed, setElapsed] = useState(0)
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const pollCountRef = useRef(0)

  const promptTextareaRef = useRef<HTMLTextAreaElement>(null)
  const promptGhostOverlayRef = useRef<HTMLDivElement>(null)

  const commitSummary = useCommitSummary(prompt)
  const promptQuality = usePromptQuality(prompt)

  const promptGhost = useGhostCompletion({
    value: prompt,
    enabled: !!refName.trim(),
    context: mode === 'ui' ? 'ui-description' : 'chat-prompt',
    related: promptQuality && promptQuality.score < 70 ? promptQuality.feedback : undefined,
    join: joinWithSpace,
  })

  // Keep the ghost overlay's scroll position in sync with the real textarea —
  // otherwise once content grows past the visible area and the field
  // auto-scrolls, the overlay (a separate absolutely-positioned element)
  // stays put and the ghost text renders overlapping already-typed content.
  // requestAnimationFrame (not a plain effect) so this reads the scroll
  // offset only after the browser has finished its own caret-follow scroll.
  function syncPromptGhostScroll() {
    requestAnimationFrame(() => {
      const textarea = promptTextareaRef.current
      const overlay = promptGhostOverlayRef.current
      if (!textarea || !overlay) return

      overlay.scrollTop = textarea.scrollTop

      // The overlay's mirrored content includes the not-yet-accepted suffix,
      // so it can be one wrapped line taller than the real textarea's own
      // content. Copying the textarea's scroll position verbatim then leaves
      // that extra line sitting just past the visible bottom edge — clipped,
      // not just unstyled. Nudge both down together so the suggestion is
      // never rendered somewhere the user can't see it.
      const overflow = overlay.scrollHeight - overlay.clientHeight - overlay.scrollTop
      if (overflow > 0) {
        textarea.scrollTop += overflow
        overlay.scrollTop += overflow
      }
    })
  }

  useEffect(syncPromptGhostScroll, [prompt, promptGhost.suffix])

  // Move the caret to the end of the newly-accepted text after accepting a
  // suggestion, so typing continues after it, not from the old (now
  // mid-string) cursor spot.
  function acceptPromptGhost() {
    const next = promptGhost.accept()
    if (next === null) return
    setPrompt(next)
    requestAnimationFrame(() => {
      const el = promptTextareaRef.current
      if (el) {
        el.selectionStart = el.selectionEnd = next.length
        el.scrollTop = el.scrollHeight
      }
      syncPromptGhostScroll()
    })
  }

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

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!refName.trim() || !prompt.trim() || !submittedBy.trim()) return
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
          commit_message: (commitSummary || prompt).trim(),
          prompt_type: mode === 'ui' ? 'generated-ui' : 'chat',
          parent_hash: parentHash,
        }),
      })
      const data = await res.json()
      if (!res.ok) { setError(data.detail ?? 'Submission failed.'); return }

      const ref = refName.trim()
      const contentHash: string = data.content_hash
      setRefName(''); setPrompt('')
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
    <div className="p-8 max-w-2xl mx-auto h-full overflow-y-auto">
      <div className="bg-white dark:bg-slate-800/60 border border-gray-200 dark:border-slate-700 rounded-2xl p-8 shadow-sm">
        <h1 className="text-gray-900 dark:text-white font-semibold text-xl">Generate Your App</h1>
        <p className="text-gray-500 dark:text-slate-400 text-sm mt-1 mb-6">
          Pick what you're building and describe it in plain language — we'll automatically test it and roll it out once it's ready.
        </p>

        <form onSubmit={handleSubmit} className="space-y-4">
          <Field id="submit-type" label="Type">
            <select
              id="submit-type"
              className="input"
              value={mode}
              onChange={e => { setMode(e.target.value as 'chat' | 'ui'); setPrompt('') }}
            >
              <option value="chat">Chat Assistant</option>
              <option value="ui">Generate UI App</option>
            </select>
          </Field>

          <Field id="submit-ref-name" label="Site Name">
            <input
              id="submit-ref-name"
              className="input"
              required
              placeholder={mode === 'ui' ? 'e.g. my-calculator' : 'e.g. customer-bot'}
              value={refName}
              onChange={e => setRefName(e.target.value)}
            />
          </Field>

          <Field id="submit-prompt" label="Describe What to Build">
            <div className={`relative w-full bg-gray-50 dark:bg-slate-900/60 border border-gray-300 dark:border-slate-600 rounded-lg focus-within:border-indigo-500 dark:focus-within:border-indigo-400 transition-colors ${!refName.trim() ? 'opacity-50' : ''}`}>
              <div
                ref={promptGhostOverlayRef}
                aria-hidden="true"
                className="absolute inset-0 px-3 py-2.5 text-sm whitespace-pre-wrap break-words overflow-hidden pointer-events-none"
              >
                <span className="invisible">{prompt}</span>
                <span className="text-gray-400 dark:text-slate-500">{promptGhost.suffix}</span>
              </div>

              <textarea
                id="submit-prompt"
                ref={promptTextareaRef}
                rows={7}
                className="relative z-10 w-full bg-transparent px-3 py-2.5 text-sm text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-slate-500 focus:outline-none resize-none disabled:cursor-not-allowed [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
                required
                disabled={!refName.trim()}
                placeholder={
                  !refName.trim()
                    ? 'Enter a Site Name first…'
                    : mode === 'ui'
                      ? 'e.g. Create a calculator with basic arithmetic operations'
                      : 'You are a helpful assistant that…'
                }
                value={prompt}
                onChange={e => { setPrompt(e.target.value); promptGhost.reset() }}
                onScroll={syncPromptGhostScroll}
                onKeyDown={e => {
                  if (e.key === 'Tab' && promptGhost.suffix) {
                    e.preventDefault()
                    acceptPromptGhost()
                  }
                }}
              />
            </div>
            {promptGhost.suffix && (
              <p className="mt-1 text-xs text-gray-400 dark:text-slate-500">Press Tab to autocomplete</p>
            )}
            {!promptGhost.suffix && prompt.trim() && promptGhost.capped && (
              <p className="mt-1 text-xs text-gray-400 dark:text-slate-500">Keep typing to get more suggestions</p>
            )}
          </Field>

          <Field id="submit-commit-message" label="What changed?">
            <input
              id="submit-commit-message"
              className="input cursor-not-allowed opacity-70"
              required
              readOnly
              placeholder="The Change..."
              value={commitSummary}
            />
          </Field>

          {error && (
            <div className="flex items-center gap-2 bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900/50 rounded-lg px-3 py-2">
              <svg className="w-4 h-4 text-red-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0z" />
              </svg>
              <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
            </div>
          )}

          <p className="text-xs text-gray-500 dark:text-slate-500">
            Submitting as <span className="font-medium text-gray-700 dark:text-slate-300">{submittedBy}</span>
          </p>

          <button
            type="submit"
            disabled={loading || !refName.trim() || !prompt.trim() || !submittedBy.trim()}
            className="w-full py-2.5 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-40 text-white text-sm font-semibold rounded-lg transition-colors flex items-center justify-center gap-2"
          >
            {loading && <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />}
            {loading ? 'Submitting…' : 'Create your App'}
          </button>
        </form>
      </div>

      {pipeline.stage !== 'idle' && (
        <div className="mt-6 rounded-xl border border-gray-200 dark:border-slate-700 bg-gray-50 dark:bg-slate-900/60 p-5 space-y-3">
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

function Field({ id, label, children }: { id: string; label: string; children: React.ReactNode }) {
  return (
    <div>
      <label htmlFor={id} className="block text-xs text-gray-500 dark:text-slate-500 mb-1.5 uppercase tracking-wider">{label}</label>
      {children}
    </div>
  )
}
