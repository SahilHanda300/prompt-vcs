import { useState } from 'react'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { Sidebar } from './components/Sidebar/Sidebar'
import { SiteView } from './pages/SiteView'
import { DashboardPage } from './pages/DashboardPage'
import { QAFailuresPage } from './pages/QAFailuresPage'
import { HistoryPage } from './pages/HistoryPage'
import { CompliancePage } from './pages/CompliancePage'
import { SubmitPage } from './pages/SubmitPage'

const queryClient = new QueryClient()

export default function App() {
  const [sidebarOpen, setSidebarOpen] = useState(false)

  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <div className="flex h-screen bg-slate-900 text-slate-100 overflow-hidden">

          {/* Mobile top bar */}
          <div className="md:hidden fixed top-0 left-0 right-0 z-30 h-12 bg-[#0d0d14] border-b border-white/5 flex items-center px-4 gap-3 shrink-0">
            <button
              onClick={() => setSidebarOpen(true)}
              className="p-1.5 rounded hover:bg-white/10 transition-colors"
              aria-label="Open menu"
            >
              <svg className="w-5 h-5 text-slate-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
              </svg>
            </button>
            <span className="text-white font-semibold text-sm tracking-tight">PromptVCS</span>
          </div>

          {/* Mobile backdrop */}
          {sidebarOpen && (
            <div
              className="md:hidden fixed inset-0 bg-black/60 z-40"
              onClick={() => setSidebarOpen(false)}
            />
          )}

          <Sidebar isOpen={sidebarOpen} onClose={() => setSidebarOpen(false)} />

          <main className="flex-1 overflow-hidden mt-12 md:mt-0">
            <Routes>
              <Route path="/" element={<Navigate to="/dashboard" replace />} />
              <Route path="/dashboard" element={<DashboardPage />} />
              <Route path="/dashboard/failures" element={<QAFailuresPage />} />
              <Route path="/dashboard/history" element={<HistoryPage />} />
              <Route path="/dashboard/compliance" element={<CompliancePage />} />
              <Route path="/submit" element={<SubmitPage />} />
              <Route path="/sites/:name" element={<SiteView />} />
            </Routes>
          </main>

        </div>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
