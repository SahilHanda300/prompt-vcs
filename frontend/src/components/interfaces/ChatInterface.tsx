import { useState, useRef, useEffect } from 'react'
import { API_URL } from '../../lib/api'
import { useSiteStore } from '../../stores/siteStore'
import type { SiteListItem } from '../../types'

interface Props {
  site: SiteListItem
}

// ── Ting sound via Web Audio API ─────────────────────────────────────────────
function playTing() {
  try {
    const ctx = new AudioContext()
    const osc = ctx.createOscillator()
    const gain = ctx.createGain()
    osc.connect(gain)
    gain.connect(ctx.destination)
    osc.type = 'sine'
    osc.frequency.setValueAtTime(880, ctx.currentTime)
    osc.frequency.exponentialRampToValueAtTime(660, ctx.currentTime + 0.15)
    gain.gain.setValueAtTime(0.25, ctx.currentTime)
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.6)
    osc.start(ctx.currentTime)
    osc.stop(ctx.currentTime + 0.6)
  } catch { /* AudioContext blocked (e.g. no user gesture yet) */ }
}

// ── Typewriter component (only for currently-streaming messages) ───────────────
function TypewriterText({ content, streaming }: { content: string; streaming?: boolean }) {
  const [pos, setPos] = useState(0)
  const contentRef = useRef(content)
  contentRef.current = content

  useEffect(() => {
    const timer = setInterval(() => {
      setPos(prev => (prev < contentRef.current.length ? prev + 1 : prev))
    }, 12)
    return () => clearInterval(timer)
  }, [])

  const displayed = content.slice(0, pos)
  const showCursor = streaming || pos < content.length

  return (
    <>
      {displayed}
      {showCursor && (
        <span className="inline-block w-1.5 h-3.5 bg-slate-400 ml-0.5 animate-pulse" />
      )}
    </>
  )
}

// ── Main component ────────────────────────────────────────────────────────────
export function ChatInterface({ site }: Props) {
  const {
    userName, setUserName,
    getSession, addMessage, appendToLastMessage, setLastMessageStreaming, clearSession, setMessages,
  } = useSiteStore()

  const [nameInput, setNameInput] = useState('')
  const [input, setInput] = useState('')
  const [isStreaming, setIsStreaming] = useState(false)
  const [historyLoading, setHistoryLoading] = useState(false)
  const bottomRef = useRef<HTMLDivElement>(null)
  const prevStreamingRef = useRef(false)
  const loadedKeysRef = useRef<Set<string>>(new Set())

  const sessionKey = userName ? `${userName}:${site.refname}` : ''
  const session = userName ? getSession(sessionKey) : null

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [session?.messages])

  // Play ting when bot starts responding
  useEffect(() => {
    if (isStreaming && !prevStreamingRef.current) playTing()
    prevStreamingRef.current = isStreaming
  }, [isStreaming])

  // Auto-load history when navigating to a new site with username already set
  useEffect(() => {
    if (!userName || loadedKeysRef.current.has(sessionKey)) return
    loadedKeysRef.current.add(sessionKey)
    loadHistory(userName, sessionKey)
  }, [userName, sessionKey])

  async function loadHistory(name: string, key: string) {
    setHistoryLoading(true)
    try {
      const res = await fetch(`${API_URL}/chat/${site.refname}/history/${encodeURIComponent(name)}`)
      if (res.ok) {
        const history: { role: 'user' | 'assistant'; content: string }[] = await res.json()
        setMessages(key, history)
      } else {
        setMessages(key, [])
      }
    } catch {
      setMessages(key, [])
    } finally {
      setHistoryLoading(false)
    }
  }

  async function handleSetName(e: React.FormEvent) {
    e.preventDefault()
    const name = nameInput.trim()
    if (!name) return
    const key = `${name}:${site.refname}`
    loadedKeysRef.current.add(key)
    setUserName(name)
    await loadHistory(name, key)
  }

  async function sendMessage() {
    if (!session) return
    const text = input.trim()
    if (!text || isStreaming) return
    setInput('')
    setIsStreaming(true)

    addMessage(sessionKey, { role: 'user', content: text })
    addMessage(sessionKey, { role: 'assistant', content: '', streaming: true })

    try {
      const res = await fetch(`${API_URL}/chat/${site.refname}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          session_id: session.sessionId,
          user_message: text,
          username: userName,
        }),
      })
      if (!res.ok || !res.body) throw new Error('Chat request failed')

      const reader = res.body.getReader()
      const decoder = new TextDecoder()
      while (true) {
        const { done, value } = await reader.read()
        if (done) break
        appendToLastMessage(sessionKey, decoder.decode(value))
      }
    } catch {
      appendToLastMessage(sessionKey, '\n\n[Error: could not get response]')
    } finally {
      setLastMessageStreaming(sessionKey, false)
      setIsStreaming(false)
    }
  }

  // ── Name prompt ───────────────────────────────────────────────────────────
  if (!userName) {
    return (
      <div className="flex flex-col h-full items-center justify-center p-8">
        <div className="w-full max-w-sm">
          <h2 className="text-white font-semibold text-lg mb-1">{site.refname}</h2>
          <p className="text-slate-400 text-sm mb-6">
            Enter your name to start chatting. Your history will be saved.
          </p>
          <form onSubmit={handleSetName} className="space-y-3">
            <input
              autoFocus
              className="w-full bg-slate-800 border border-slate-600 rounded px-3 py-2 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500"
              placeholder="e.g. sahil-handa"
              value={nameInput}
              onChange={e => setNameInput(e.target.value)}
            />
            <button
              type="submit"
              disabled={!nameInput.trim()}
              className="w-full py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-30 text-white text-sm font-medium rounded transition-colors"
            >
              Start chatting
            </button>
          </form>
        </div>
      </div>
    )
  }

  // ── Chat UI ───────────────────────────────────────────────────────────────
  return (
    <div className="flex flex-col h-full">
      <div className="border-b border-slate-700 p-4 flex items-start justify-between">
        <div>
          <h2 className="text-white font-semibold">{site.refname}</h2>
          <p className="text-slate-400 text-sm">
            Chatting as <span className="text-indigo-400">{userName}</span>
          </p>
        </div>
        {session && session.messages.length > 0 && (
          <button
            onClick={() => clearSession(sessionKey)}
            className="text-xs text-slate-500 hover:text-red-400 transition-colors mt-0.5"
          >
            Clear chat
          </button>
        )}
      </div>

      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {historyLoading && (
          <p className="text-slate-500 text-sm text-center mt-8 animate-pulse">Loading your history…</p>
        )}
        {!historyLoading && session?.messages.length === 0 && (
          <p className="text-slate-500 text-sm text-center mt-8">
            {site.inputplaceholder ?? 'Start a conversation'}
          </p>
        )}
        {session?.messages.map((msg, i) => (
          <div key={i} className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}>
            <div
              className={`max-w-[75%] rounded-lg px-4 py-2 text-sm whitespace-pre-wrap ${
                msg.role === 'user'
                  ? 'bg-indigo-600 text-white'
                  : 'bg-slate-700 text-slate-100'
              }`}
            >
              {msg.streaming ? (
                <TypewriterText content={msg.content} streaming />
              ) : (
                msg.content
              )}
            </div>
          </div>
        ))}
        <div ref={bottomRef} />
      </div>

      <div className="border-t border-slate-700 p-4 flex gap-2 items-end">
        <textarea
          rows={1}
          className="flex-1 bg-slate-800 border border-slate-600 rounded px-3 py-2 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 resize-none overflow-y-auto max-h-36"
          placeholder={site.inputplaceholder ?? 'Type a message… (Shift+Enter for new line)'}
          value={input}
          onChange={e => {
            setInput(e.target.value)
            e.target.style.height = 'auto'
            e.target.style.height = `${Math.min(e.target.scrollHeight, 144)}px`
          }}
          onKeyDown={e => {
            if (e.key === 'Enter' && !e.shiftKey) {
              e.preventDefault()
              sendMessage()
            }
          }}
          disabled={isStreaming}
        />
        <button
          onClick={sendMessage}
          disabled={isStreaming || !input.trim()}
          className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-40 text-white rounded text-sm font-medium"
        >
          Send
        </button>
      </div>
    </div>
  )
}
