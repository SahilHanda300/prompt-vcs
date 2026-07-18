import { useState } from 'react'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ThemeProvider } from './lib/ThemeContext'
import { AuthProvider, useAuth } from './lib/AuthContext'
import { Sidebar } from './components/Sidebar/Sidebar'
import { SiteView } from './pages/SiteView'
import { DashboardPage } from './pages/DashboardPage'
import { QAFailuresPage } from './pages/QAFailuresPage'
import { HistoryPage } from './pages/HistoryPage'
import { CompliancePage } from './pages/CompliancePage'
import { SubmitPage } from './pages/SubmitPage'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'

const queryClient = new QueryClient()

function RequireAuth({ children }: { children: React.ReactNode }) {
  const { user } = useAuth()
  if (!user) return <Navigate to="/login" replace />
  return <>{children}</>
}

function AppShell() {
  const [sidebarOpen, setSidebarOpen] = useState(false)

  return (
    <div className="flex h-screen bg-gray-50 dark:bg-slate-900 text-gray-900 dark:text-slate-100 overflow-hidden">
      {/* Mobile top bar */}
      <div className="md:hidden fixed top-0 left-0 right-0 z-30 h-12 bg-white dark:bg-[#0d0d14] border-b border-gray-200 dark:border-white/5 flex items-center px-4 gap-3 shrink-0">
        <button
          onClick={() => setSidebarOpen(true)}
          className="p-1.5 rounded hover:bg-gray-100 dark:hover:bg-white/10 transition-colors"
          aria-label="Open menu"
        >
          <svg className="w-5 h-5 text-gray-500 dark:text-slate-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
          </svg>
        </button>
        <span className="text-gray-900 dark:text-white font-semibold text-sm tracking-tight">PromptVCS</span>
      </div>

      {/* Mobile backdrop */}
      {sidebarOpen && (
        <div className="md:hidden fixed inset-0 bg-black/60 z-40" onClick={() => setSidebarOpen(false)} />
      )}

      <Sidebar isOpen={sidebarOpen} onClose={() => setSidebarOpen(false)} />

      <main className="flex-1 overflow-hidden mt-12 md:mt-0">
        <Routes>
          <Route path="/dashboard"            element={<DashboardPage />} />
          <Route path="/dashboard/failures"   element={<QAFailuresPage />} />
          <Route path="/dashboard/history"    element={<HistoryPage />} />
          <Route path="/dashboard/compliance" element={<CompliancePage />} />
          <Route path="/submit"               element={<SubmitPage />} />
          <Route path="/sites/:name"          element={<SiteView />} />
        </Routes>
      </main>
    </div>
  )
}

export default function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <QueryClientProvider client={queryClient}>
          <BrowserRouter>
            <Routes>
              {/* Public */}
              <Route path="/"         element={<HomePage />} />
              <Route path="/login"    element={<LoginPage />} />
              <Route path="/register" element={<RegisterPage />} />

              {/* Protected */}
              <Route path="/*" element={
                <RequireAuth>
                  <AppShell />
                </RequireAuth>
              } />
            </Routes>
          </BrowserRouter>
        </QueryClientProvider>
      </AuthProvider>
    </ThemeProvider>
  )
}
