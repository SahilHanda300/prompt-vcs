import { useNavigate } from 'react-router-dom'
import { useTheme } from '../lib/ThemeContext'

// ── SVG Illustrations ─────────────────────────────────────────────────────────

function HeroIllustration() {
  return (
    <svg viewBox="0 0 380 240" fill="none" xmlns="http://www.w3.org/2000/svg" className="w-full max-w-sm mx-auto">
      {/* Card 1 — oldest, background */}
      <rect x="48" y="16" width="288" height="148" rx="14" className="fill-gray-100 dark:fill-slate-800" stroke="none" />
      <rect x="48" y="16" width="288" height="148" rx="14" className="stroke-gray-200 dark:stroke-slate-700" strokeWidth="1.5" fill="none" />
      <text x="72" y="45" className="fill-gray-300 dark:fill-slate-600" fontSize="11" fontFamily="monospace">v1 · Initial version</text>
      <rect x="72" y="56" width="120" height="7" rx="3" className="fill-gray-200 dark:fill-slate-700" />
      <rect x="72" y="70" width="80" height="7" rx="3" className="fill-gray-200 dark:fill-slate-700" />

      {/* Card 2 — middle */}
      <rect x="24" y="44" width="288" height="148" rx="14" className="fill-amber-50 dark:fill-amber-950/40" stroke="none" />
      <rect x="24" y="44" width="288" height="148" rx="14" className="stroke-amber-200 dark:stroke-amber-800/50" strokeWidth="1.5" fill="none" />
      <text x="48" y="73" className="fill-gray-500 dark:fill-slate-400" fontSize="11" fontFamily="monospace">v2 · Added stricter tone</text>
      <rect x="280" y="60" width="16" height="16" rx="8" className="fill-amber-100 dark:fill-amber-900/60" />
      <circle cx="288" cy="68" r="3" className="fill-amber-400" />
      <rect x="48" y="84" width="140" height="7" rx="3" className="fill-amber-100 dark:fill-amber-900/40" />
      <rect x="48" y="98" width="100" height="7" rx="3" className="fill-amber-100 dark:fill-amber-900/40" />

      {/* Card 3 — front, live */}
      <rect x="0" y="72" width="288" height="148" rx="14" className="fill-white dark:fill-slate-800/90" stroke="none"
        style={{ filter: 'drop-shadow(0 4px 24px rgba(99,102,241,0.10))' }} />
      <rect x="0" y="72" width="288" height="148" rx="14" className="stroke-indigo-200 dark:stroke-indigo-500/40" strokeWidth="1.5" fill="none" />

      {/* live badge */}
      <rect x="24" y="95" width="42" height="18" rx="9" className="fill-emerald-100 dark:fill-emerald-500/20" />
      <circle cx="34" cy="104" r="3" className="fill-emerald-500" />
      <text x="40" y="108" className="fill-emerald-700 dark:fill-emerald-400" fontSize="9" fontWeight="700" letterSpacing="0.5">LIVE</text>

      <text x="24" y="86" className="fill-gray-800 dark:fill-slate-200" fontSize="11" fontFamily="monospace" fontWeight="600">v3 · Fixed edge cases</text>

      {/* chat bubbles */}
      <rect x="24" y="120" width="140" height="28" rx="12" className="fill-indigo-100 dark:fill-indigo-500/20" />
      <text x="36" y="138" className="fill-indigo-700 dark:fill-indigo-300" fontSize="10">How can I help you?</text>

      <rect x="100" y="156" width="160" height="28" rx="12" className="fill-gray-100 dark:fill-slate-700" />
      <text x="112" y="174" className="fill-gray-600 dark:fill-slate-300" fontSize="10">Tell me about pricing</text>

      {/* score badge */}
      <rect x="232" y="91" width="40" height="18" rx="6" className="fill-green-100 dark:fill-green-900/40" />
      <text x="240" y="104" className="fill-green-700 dark:fill-green-400" fontSize="9" fontWeight="600" fontFamily="monospace">0.94</text>
    </svg>
  )
}

function IconHistory() {
  return (
    <svg viewBox="0 0 40 40" fill="none" className="w-10 h-10">
      <rect width="40" height="40" rx="10" className="fill-indigo-100 dark:fill-indigo-500/20" />
      <circle cx="20" cy="20" r="8" className="stroke-indigo-500 dark:stroke-indigo-400" strokeWidth="1.8" fill="none" />
      <path d="M20 14v6l4 2" className="stroke-indigo-500 dark:stroke-indigo-400" strokeWidth="1.8" strokeLinecap="round" />
      <path d="M13 10l-2 3 3 1" className="stroke-indigo-400 dark:stroke-indigo-500" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

function IconShield() {
  return (
    <svg viewBox="0 0 40 40" fill="none" className="w-10 h-10">
      <rect width="40" height="40" rx="10" className="fill-emerald-100 dark:fill-emerald-500/20" />
      <path d="M20 10l8 3v6c0 4-3.5 7.5-8 9-4.5-1.5-8-5-8-9v-6l8-3z"
        className="stroke-emerald-600 dark:stroke-emerald-400" strokeWidth="1.8" fill="none" strokeLinejoin="round" />
      <path d="M16 20l3 3 5-5" className="stroke-emerald-600 dark:stroke-emerald-400" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

function IconUndo() {
  return (
    <svg viewBox="0 0 40 40" fill="none" className="w-10 h-10">
      <rect width="40" height="40" rx="10" className="fill-violet-100 dark:fill-violet-500/20" />
      <path d="M14 16h-4v-4" className="stroke-violet-600 dark:stroke-violet-400" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M10 16a10 10 0 1 1 2 6" className="stroke-violet-600 dark:stroke-violet-400" strokeWidth="1.8" strokeLinecap="round" fill="none" />
    </svg>
  )
}

function IconCompare() {
  return (
    <svg viewBox="0 0 40 40" fill="none" className="w-10 h-10">
      <rect width="40" height="40" rx="10" className="fill-amber-100 dark:fill-amber-500/20" />
      <rect x="9" y="12" width="9" height="16" rx="3" className="stroke-amber-600 dark:stroke-amber-400" strokeWidth="1.6" fill="none" />
      <rect x="22" y="12" width="9" height="16" rx="3" className="stroke-amber-600 dark:stroke-amber-400" strokeWidth="1.6" fill="none" />
      <path d="M18 20h4" className="stroke-amber-500 dark:stroke-amber-400" strokeWidth="1.6" strokeLinecap="round" />
    </svg>
  )
}

function StepDot({ n }: { n: number }) {
  return (
    <div className="w-8 h-8 rounded-full bg-indigo-600 dark:bg-indigo-500 text-white flex items-center justify-center text-sm font-bold shrink-0">
      {n}
    </div>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────

export function HomePage() {
  const navigate = useNavigate()
  const { isDark, toggle } = useTheme()

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-[#0a0a12] text-gray-900 dark:text-slate-100 overflow-y-auto">

      {/* Nav */}
      <nav className="sticky top-0 z-20 bg-white/80 dark:bg-[#0a0a12]/80 backdrop-blur border-b border-gray-200 dark:border-white/5">
        <div className="max-w-5xl mx-auto px-6 h-14 flex items-center justify-between">
          <span className="font-bold tracking-tight text-gray-900 dark:text-white">PromptVCS</span>
          <div className="flex items-center gap-3">
            <button
              onClick={toggle}
              title={isDark ? 'Switch to light mode' : 'Switch to dark mode'}
              className="p-1.5 rounded text-gray-400 dark:text-slate-500 hover:text-gray-700 dark:hover:text-slate-300 hover:bg-gray-100 dark:hover:bg-white/5 transition-colors"
            >
              {isDark ? (
                <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 3v1m0 16v1m8.66-9h-1M4.34 12h-1m15.07-6.07-.7.7M6.34 17.66l-.7.7m12.73 0-.7-.7M6.34 6.34l-.7-.7M12 5a7 7 0 1 0 0 14A7 7 0 0 0 12 5z" />
                </svg>
              ) : (
                <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 24 24">
                  <path d="M21 12.79A9 9 0 1 1 11.21 3a7 7 0 0 0 9.79 9.79z" />
                </svg>
              )}
            </button>
            <button
              onClick={() => navigate('/login')}
              className="text-sm text-gray-500 dark:text-slate-400 hover:text-gray-900 dark:hover:text-white transition-colors"
            >
              Sign in
            </button>
            <button
              onClick={() => navigate('/login')}
              className="px-4 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white text-sm font-semibold rounded-lg transition-colors"
            >
              Get Started
            </button>
          </div>
        </div>
      </nav>

      {/* Hero */}
      <section className="max-w-5xl mx-auto px-6 pt-20 pb-16">
        <div className="grid md:grid-cols-2 gap-12 items-center">
          <div>
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full border border-indigo-300 dark:border-indigo-500/30 bg-indigo-50 dark:bg-indigo-500/10 text-indigo-600 dark:text-indigo-300 text-xs font-semibold tracking-wide mb-6">
              <span className="w-1.5 h-1.5 rounded-full bg-indigo-500 animate-pulse" />
              Now live
            </div>

            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-gray-900 dark:text-white leading-tight mb-5">
              Your AI assistant,{' '}
              <span className="text-transparent bg-clip-text bg-gradient-to-r from-indigo-500 to-violet-500">
                always improving
              </span>
            </h1>

            <p className="text-lg text-gray-500 dark:text-slate-400 leading-relaxed mb-8">
              PromptVCS keeps a complete history of every change made to your AI assistant.
              Every update is tested automatically — so you always know what's live, what changed, and how to undo it.
            </p>

            <div className="flex flex-wrap gap-3">
              <button
                onClick={() => navigate('/login')}
                className="px-6 py-3 bg-indigo-600 hover:bg-indigo-500 text-white font-semibold rounded-xl transition-colors"
              >
                Sign in →
              </button>
              <button
                onClick={() => navigate('/login')}
                className="px-6 py-3 bg-white dark:bg-white/5 hover:bg-gray-100 dark:hover:bg-white/10 border border-gray-200 dark:border-white/10 text-gray-700 dark:text-slate-300 font-medium rounded-xl transition-colors"
              >
                View dashboard
              </button>
            </div>
          </div>

          <div className="hidden md:block">
            <HeroIllustration />
          </div>
        </div>
      </section>

      {/* Social proof strip */}
      <section className="border-y border-gray-100 dark:border-white/5 bg-white dark:bg-white/2 py-6">
        <div className="max-w-5xl mx-auto px-6">
          <div className="flex flex-wrap justify-center gap-8 text-center">
            {[
              { value: '100%', label: 'Automated testing' },
              { value: '< 1s',  label: 'Rollback time' },
              { value: 'Full',  label: 'Version history' },
              { value: 'Zero',  label: 'Manual approvals' },
            ].map(stat => (
              <div key={stat.label}>
                <p className="text-2xl font-bold text-indigo-600 dark:text-indigo-400">{stat.value}</p>
                <p className="text-xs text-gray-400 dark:text-slate-500 mt-0.5">{stat.label}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* How it works */}
      <section className="max-w-5xl mx-auto px-6 py-20">
        <div className="text-center mb-12">
          <p className="text-xs text-gray-400 dark:text-slate-500 uppercase tracking-widest font-semibold mb-2">How it works</p>
          <h2 className="text-2xl font-bold text-gray-900 dark:text-white">Three simple steps</h2>
        </div>

        <div className="grid md:grid-cols-3 gap-8">
          {[
            {
              step: 1,
              title: 'Write your AI instructions',
              body: 'Describe how you want your AI assistant to behave. Give it a name and a purpose — like a customer support bot or a writing helper.',
              visual: (
                <div className="rounded-xl border border-gray-200 dark:border-slate-700 bg-gray-50 dark:bg-slate-800/60 p-4">
                  <p className="text-[10px] text-gray-400 dark:text-slate-500 uppercase tracking-wider mb-2">Your instructions</p>
                  <p className="text-xs text-gray-700 dark:text-slate-300 leading-relaxed">
                    "You are a friendly customer support agent for Acme Co. Always greet the user by name and offer to help…"
                  </p>
                </div>
              ),
            },
            {
              step: 2,
              title: 'We test it automatically',
              body: 'As soon as you save a change, PromptVCS runs it through a set of test questions. An AI judge scores the answers. No action needed from you.',
              visual: (
                <div className="rounded-xl border border-gray-200 dark:border-slate-700 bg-gray-50 dark:bg-slate-800/60 p-4 space-y-2">
                  {[
                    { q: 'How do I return an item?', ok: true },
                    { q: 'What are your opening hours?', ok: true },
                    { q: 'Can I change my order?', ok: false },
                  ].map(tc => (
                    <div key={tc.q} className="flex items-center gap-2">
                      <span className={`w-4 h-4 rounded-full flex items-center justify-center text-[9px] font-bold shrink-0 ${tc.ok ? 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-600 dark:text-emerald-400' : 'bg-red-100 dark:bg-red-900/40 text-red-600 dark:text-red-400'}`}>
                        {tc.ok ? '✓' : '✗'}
                      </span>
                      <span className="text-[10px] text-gray-500 dark:text-slate-400 truncate">{tc.q}</span>
                    </div>
                  ))}
                </div>
              ),
            },
            {
              step: 3,
              title: "It goes live — or you're told why not",
              body: 'If the test passes, your update goes live automatically. If it fails, you get a clear reason and the previous version stays live unchanged.',
              visual: (
                <div className="space-y-2">
                  <div className="rounded-xl border border-emerald-200 dark:border-emerald-800/50 bg-emerald-50 dark:bg-emerald-900/20 p-3 flex items-center gap-2">
                    <span className="w-5 h-5 rounded-full bg-emerald-500 flex items-center justify-center text-white text-[10px] font-bold shrink-0">✓</span>
                    <div>
                      <p className="text-xs font-semibold text-emerald-700 dark:text-emerald-400">Update is live</p>
                      <p className="text-[10px] text-emerald-600/70 dark:text-emerald-500">Your users see the new version now</p>
                    </div>
                  </div>
                  <div className="rounded-xl border border-red-200 dark:border-red-800/50 bg-red-50 dark:bg-red-900/20 p-3 flex items-center gap-2">
                    <span className="w-5 h-5 rounded-full bg-red-400 flex items-center justify-center text-white text-[10px] font-bold shrink-0">✗</span>
                    <div>
                      <p className="text-xs font-semibold text-red-700 dark:text-red-400">Update blocked</p>
                      <p className="text-[10px] text-red-600/70 dark:text-red-500">Answers were worse than before</p>
                    </div>
                  </div>
                </div>
              ),
            },
          ].map(step => (
            <div key={step.step} className="flex flex-col gap-4">
              <div className="flex items-center gap-3">
                <StepDot n={step.step} />
                <h3 className="font-semibold text-gray-900 dark:text-white text-sm leading-snug">{step.title}</h3>
              </div>
              <p className="text-sm text-gray-500 dark:text-slate-400 leading-relaxed">{step.body}</p>
              {step.visual}
            </div>
          ))}
        </div>
      </section>

      {/* Features */}
      <section className="bg-white dark:bg-white/2 border-y border-gray-100 dark:border-white/5 py-20">
        <div className="max-w-5xl mx-auto px-6">
          <div className="text-center mb-12">
            <p className="text-xs text-gray-400 dark:text-slate-500 uppercase tracking-widest font-semibold mb-2">Features</p>
            <h2 className="text-2xl font-bold text-gray-900 dark:text-white">Everything you need to trust your AI</h2>
          </div>

          <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-5">
            {[
              {
                icon: <IconHistory />,
                title: 'Full version history',
                body: 'Every change is saved forever. See exactly what your AI said last week, last month, or on day one.',
              },
              {
                icon: <IconShield />,
                title: 'Automatic quality checks',
                body: 'Your AI is tested on real questions every time you update it. Bad changes never reach your users.',
              },
              {
                icon: <IconUndo />,
                title: 'Undo any change',
                body: 'Made a mistake? Restore any previous version in under a second — no IT ticket required.',
              },
              {
                icon: <IconCompare />,
                title: 'See what changed',
                body: 'Compare any two versions side by side. Spot the exact word or sentence that made a difference.',
              },
            ].map(f => (
              <div
                key={f.title}
                className="rounded-xl border border-gray-200 dark:border-white/8 bg-gray-50 dark:bg-white/3 hover:bg-gray-100 dark:hover:bg-white/5 p-5 transition-colors"
              >
                {f.icon}
                <p className="text-gray-900 dark:text-white font-semibold text-sm mt-3 mb-1.5">{f.title}</p>
                <p className="text-gray-500 dark:text-slate-500 text-xs leading-relaxed">{f.body}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Before / After */}
      <section className="max-w-5xl mx-auto px-6 py-20">
        <div className="text-center mb-12">
          <p className="text-xs text-gray-400 dark:text-slate-500 uppercase tracking-widest font-semibold mb-2">The problem we solve</p>
          <h2 className="text-2xl font-bold text-gray-900 dark:text-white">Stop flying blind with your AI</h2>
          <p className="text-gray-500 dark:text-slate-400 text-sm mt-3 max-w-xl mx-auto">
            Most teams update their AI assistants by guesswork. PromptVCS gives you data, history, and control.
          </p>
        </div>

        <div className="grid sm:grid-cols-2 gap-6">
          {/* Without */}
          <div className="rounded-2xl border border-red-200 dark:border-red-900/40 bg-red-50 dark:bg-red-950/20 p-6">
            <p className="text-xs font-bold uppercase tracking-widest text-red-500 dark:text-red-400 mb-4">Without PromptVCS</p>
            <ul className="space-y-3">
              {[
                'Nobody knows what the AI was told last month',
                'A small tweak makes the AI start giving wrong answers',
                'You can\'t tell which version was working best',
                'Rolling back means digging through old notes',
                'Users complain before the team even notices',
              ].map(item => (
                <li key={item} className="flex items-start gap-2 text-sm text-red-700 dark:text-red-300">
                  <span className="mt-0.5 shrink-0 text-red-400">✗</span>
                  {item}
                </li>
              ))}
            </ul>
          </div>

          {/* With */}
          <div className="rounded-2xl border border-emerald-200 dark:border-emerald-900/40 bg-emerald-50 dark:bg-emerald-950/20 p-6">
            <p className="text-xs font-bold uppercase tracking-widest text-emerald-600 dark:text-emerald-400 mb-4">With PromptVCS</p>
            <ul className="space-y-3">
              {[
                'Every version is saved with a timestamp and author',
                'Bad updates are blocked before they reach users',
                'Score history shows exactly which version performed best',
                'Restore any previous version in one click',
                'You\'re notified the moment quality drops',
              ].map(item => (
                <li key={item} className="flex items-start gap-2 text-sm text-emerald-700 dark:text-emerald-300">
                  <span className="mt-0.5 shrink-0 text-emerald-500">✓</span>
                  {item}
                </li>
              ))}
            </ul>
          </div>
        </div>
      </section>

      {/* CTA */}
      <section className="bg-indigo-600 dark:bg-indigo-600/90 py-16">
        <div className="max-w-5xl mx-auto px-6 text-center">
          <h2 className="text-2xl font-bold text-white mb-3">Ready to take control of your AI?</h2>
          <p className="text-indigo-200 text-sm mb-8">
            Submit your first AI assistant in under two minutes. No account needed.
          </p>
          <button
            onClick={() => navigate('/login')}
            className="px-8 py-3 bg-white hover:bg-gray-50 text-indigo-700 font-bold rounded-xl transition-colors"
          >
            Sign in →
          </button>
        </div>
      </section>

      {/* Footer */}
      <footer className="border-t border-gray-200 dark:border-white/5 py-6 text-center text-xs text-gray-400 dark:text-slate-700">
        Made by Sahil Handa &copy; 2026 · PromptVCS
      </footer>
    </div>
  )
}
