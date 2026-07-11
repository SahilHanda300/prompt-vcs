import { useState } from 'react'
import { API_URL } from '../../lib/api'
import { useSiteStore } from '../../stores/siteStore'
import type { SiteListItem } from '../../types'

interface TransformInterfaceProps {
  site: SiteListItem
}

export function TransformInterface({ site }: TransformInterfaceProps) {
  const [input, setInput] = useState('')
  const [output, setOutput] = useState('')
  const [loading, setLoading] = useState(false)
  const { getSession } = useSiteStore()
  const session = getSession(site.refname)

  async function transform() {
    if (!input.trim() || loading) return
    setLoading(true)
    setOutput('')
    try {
      const res = await fetch(`${API_URL}/chat/${site.refname}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ session_id: session.sessionId, user_message: input }),
      })
      if (!res.ok || !res.body) throw new Error('Request failed')
      const reader = res.body.getReader()
      const decoder = new TextDecoder()
      while (true) {
        const { done, value } = await reader.read()
        if (done) break
        setOutput(prev => prev + decoder.decode(value))
      }
    } catch {
      setOutput('[Error: could not transform text]')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="flex flex-col h-full p-6 gap-4">
      <h2 className="text-white font-semibold">{site.refname}</h2>
      <div className="flex gap-4 flex-1">
        <div className="flex-1 flex flex-col gap-2">
          <label className="text-sm text-slate-400">{site.inputlabel ?? 'Input'}</label>
          <textarea
            className="flex-1 bg-slate-800 border border-slate-600 rounded p-3 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 resize-none"
            placeholder={site.inputplaceholder ?? 'Enter text to transform…'}
            value={input}
            onChange={e => setInput(e.target.value)}
          />
        </div>
        <div className="flex-1 flex flex-col gap-2">
          <label className="text-sm text-slate-400">{site.outputlabel ?? 'Output'}</label>
          <div className="flex-1 bg-slate-800 border border-slate-600 rounded p-3 text-sm text-slate-100 whitespace-pre-wrap overflow-y-auto">
            {loading && !output ? <span className="text-slate-500">Transforming…</span> : output}
          </div>
        </div>
      </div>
      <button
        onClick={transform}
        disabled={loading || !input.trim()}
        className="self-end px-6 py-2 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-40 text-white rounded text-sm font-medium"
      >
        {loading ? 'Transforming…' : 'Transform'}
      </button>
    </div>
  )
}
