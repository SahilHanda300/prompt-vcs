import { useEffect } from 'react'
import { NavLink, useLocation, useNavigate } from 'react-router-dom'
import { usePRODSites } from '../../hooks/usePRODSites'
import { ScoreBadge } from '../shared/ScoreBadge'
import { useTheme } from '../../lib/ThemeContext'
import { useAuth } from '../../lib/AuthContext'

interface SidebarProps {
  isOpen: boolean
  onClose: () => void
}

function SideLink({ to, children, end, onClick }: { to: string; children: React.ReactNode; end?: boolean; onClick?: () => void }) {
  return (
    <NavLink
      to={to}
      end={end}
      onClick={onClick}
      className={({ isActive }) =>
        `block px-3 py-1.5 rounded text-sm transition-colors ${
          isActive
            ? 'bg-indigo-50 text-indigo-700 dark:bg-white/10 dark:text-white'
            : 'text-gray-600 dark:text-slate-400 hover:text-gray-900 dark:hover:text-white hover:bg-gray-100 dark:hover:bg-white/5'
        }`
      }
    >
      {children}
    </NavLink>
  )
}

function ThemeToggle() {
  const { isDark, toggle } = useTheme()
  return (
    <button
      onClick={toggle}
      title={isDark ? 'Switch to light mode' : 'Switch to dark mode'}
      className="flex items-center gap-1.5 px-2 py-1 rounded text-xs text-gray-400 dark:text-slate-600 hover:text-gray-700 dark:hover:text-slate-300 hover:bg-gray-100 dark:hover:bg-white/5 transition-colors"
    >
      {isDark ? (
        <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 3v1m0 16v1m8.66-9h-1M4.34 12h-1m15.07-6.07-.7.7M6.34 17.66l-.7.7m12.73 0-.7-.7M6.34 6.34l-.7-.7M12 5a7 7 0 1 0 0 14A7 7 0 0 0 12 5z" />
        </svg>
      ) : (
        <svg className="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 24 24">
          <path d="M21 12.79A9 9 0 1 1 11.21 3a7 7 0 0 0 9.79 9.79z" />
        </svg>
      )}
      {isDark ? 'Light' : 'Dark'}
    </button>
  )
}

export function Sidebar({ isOpen, onClose }: SidebarProps) {
  const { data: sites } = usePRODSites()
  const location = useLocation()
  const navigate = useNavigate()
  const { user, logout } = useAuth()

  useEffect(() => {
    onClose()
  }, [location.pathname])

  return (
    <aside
      className={`
        w-56 shrink-0 flex flex-col h-screen
        border-r border-gray-200 dark:border-white/5
        bg-white dark:bg-[#0d0d14]
        fixed inset-y-0 left-0 z-50 transition-transform duration-200
        md:relative md:z-auto md:translate-x-0
        ${isOpen ? 'translate-x-0' : '-translate-x-full'}
      `}
    >
      <div className="px-4 py-5 border-b border-gray-100 dark:border-white/5">
        <p className="text-gray-900 dark:text-white font-semibold tracking-tight">PromptVCS</p>
        <p className="text-gray-400 dark:text-slate-500 text-xs mt-0.5">Prompt Version Control</p>
      </div>

      <nav className="flex-1 overflow-y-auto px-2 py-3 space-y-4">
        <div className="space-y-0.5">
          <p className="px-3 pb-1 text-[10px] font-semibold uppercase tracking-widest text-gray-400 dark:text-slate-600">Workspace</p>
          <SideLink to="/submit" onClick={onClose}>Submit Prompt</SideLink>
          <SideLink to="/dashboard" end onClick={onClose}>Overview</SideLink>
          <SideLink to="/dashboard/failures" onClick={onClose}>QA Failures</SideLink>
          <SideLink to="/dashboard/history" onClick={onClose}>History</SideLink>
          <SideLink to="/dashboard/compliance" onClick={onClose}>Compliance</SideLink>
        </div>

        <div className="space-y-0.5">
          <p className="px-3 pb-1 text-[10px] font-semibold uppercase tracking-widest text-gray-400 dark:text-slate-600">
            Live Sites {sites?.length ? `(${sites.length})` : ''}
          </p>
          {sites?.map(site => (
            <NavLink
              key={site.refname}
              to={`/sites/${site.refname}`}
              onClick={onClose}
              className={({ isActive }) =>
                `block px-3 py-2 rounded transition-colors ${
                  isActive
                    ? 'bg-indigo-50 text-indigo-700 dark:bg-white/10 dark:text-white'
                    : 'text-gray-600 dark:text-slate-400 hover:text-gray-900 dark:hover:text-white hover:bg-gray-100 dark:hover:bg-white/5'
                }`
              }
            >
              <div className="flex items-center justify-between mb-1">
                <span className="text-sm truncate">{site.refname}</span>
                <span className="text-[10px] text-gray-400 dark:text-slate-600 ml-1 shrink-0">{site.prompttype}</span>
              </div>
              <div className="flex gap-1">
                <ScoreBadge score={site.goldenscore} type="golden" />
                <ScoreBadge score={site.judgescore} type="judge" />
              </div>
            </NavLink>
          ))}
          {!sites?.length && (
            <p className="px-3 text-xs text-gray-400 dark:text-slate-600">No live sites yet</p>
          )}
        </div>
      </nav>

      {/* User + controls footer */}
      <div className="px-3 py-3 border-t border-gray-100 dark:border-white/5 space-y-2">
        {user && (
          <div className="flex items-center gap-2 px-1">
            <div className="w-7 h-7 rounded-full bg-indigo-600 flex items-center justify-center text-white text-xs font-bold shrink-0">
              {user.name.charAt(0).toUpperCase()}
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-xs font-medium text-gray-800 dark:text-slate-200 truncate">{user.name}</p>
              <p className="text-[10px] text-gray-400 dark:text-slate-600 truncate">@{user.username}</p>
            </div>
            <button
              onClick={() => { logout(); navigate('/login') }}
              title="Sign out"
              className="text-gray-400 dark:text-slate-600 hover:text-red-500 dark:hover:text-red-400 transition-colors shrink-0"
            >
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2h6a2 2 0 0 1 2 2v1" />
              </svg>
            </button>
          </div>
        )}
        <div className="flex items-center justify-between px-1">
          <p className="text-[10px] text-gray-300 dark:text-slate-700">Made by Sahil Handa &copy; 2026</p>
          <ThemeToggle />
        </div>
      </div>
    </aside>
  )
}
