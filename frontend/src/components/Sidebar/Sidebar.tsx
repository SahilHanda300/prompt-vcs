import { useEffect } from 'react'
import { NavLink, useLocation } from 'react-router-dom'
import { usePRODSites } from '../../hooks/usePRODSites'
import { ScoreBadge } from '../shared/ScoreBadge'

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
          isActive ? 'bg-white/10 text-white' : 'text-slate-400 hover:text-white hover:bg-white/5'
        }`
      }
    >
      {children}
    </NavLink>
  )
}

export function Sidebar({ isOpen, onClose }: SidebarProps) {
  const { data: sites } = usePRODSites()
  const location = useLocation()

  // Close sidebar on navigation (mobile)
  useEffect(() => {
    onClose()
  }, [location.pathname])

  return (
    <aside
      className={`
        w-56 shrink-0 flex flex-col h-screen border-r border-white/5 bg-[#0d0d14]
        fixed inset-y-0 left-0 z-50 transition-transform duration-200
        md:relative md:z-auto md:translate-x-0
        ${isOpen ? 'translate-x-0' : '-translate-x-full'}
      `}
    >
      <div className="px-4 py-5 border-b border-white/5">
        <p className="text-white font-semibold tracking-tight">PromptVCS</p>
        <p className="text-slate-500 text-xs mt-0.5">Prompt Version Control</p>
      </div>

      <nav className="flex-1 overflow-y-auto px-2 py-3 space-y-4">
        <div className="space-y-0.5">
          <p className="px-3 pb-1 text-[10px] font-semibold uppercase tracking-widest text-slate-600">Workspace</p>
          <SideLink to="/submit" onClick={onClose}>Submit Prompt</SideLink>
          <SideLink to="/dashboard" end onClick={onClose}>Overview</SideLink>
          <SideLink to="/dashboard/failures" onClick={onClose}>QA Failures</SideLink>
          <SideLink to="/dashboard/history" onClick={onClose}>History</SideLink>
          <SideLink to="/dashboard/compliance" onClick={onClose}>Compliance</SideLink>
        </div>

        <div className="space-y-0.5">
          <p className="px-3 pb-1 text-[10px] font-semibold uppercase tracking-widest text-slate-600">
            Live Sites {sites?.length ? `(${sites.length})` : ''}
          </p>
          {sites?.map(site => (
            <NavLink
              key={site.refname}
              to={`/sites/${site.refname}`}
              onClick={onClose}
              className={({ isActive }) =>
                `block px-3 py-2 rounded transition-colors ${
                  isActive ? 'bg-white/10 text-white' : 'text-slate-400 hover:text-white hover:bg-white/5'
                }`
              }
            >
              <div className="flex items-center justify-between mb-1">
                <span className="text-sm truncate">{site.refname}</span>
                <span className="text-[10px] text-slate-600 ml-1 shrink-0">{site.prompttype}</span>
              </div>
              <div className="flex gap-1">
                <ScoreBadge score={site.goldenscore} type="golden" />
                <ScoreBadge score={site.judgescore} type="judge" />
              </div>
            </NavLink>
          ))}
          {!sites?.length && (
            <p className="px-3 text-xs text-slate-600">No live sites yet</p>
          )}
        </div>
      </nav>

      <div className="px-4 py-3 border-t border-white/5">
        <p className="text-[10px] text-slate-700">Made by Sahil Handa &copy; 2026</p>
      </div>
    </aside>
  )
}
