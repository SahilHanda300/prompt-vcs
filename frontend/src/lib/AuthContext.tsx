import { createContext, useContext, useState } from 'react'
import { API_URL } from './api'

export interface AuthUser {
  userId: string
  name: string
  username: string
}

interface AuthContextValue {
  user: AuthUser | null
  login: (username: string, password: string) => Promise<string | null>
  register: (name: string, username: string, password: string) => Promise<string | null>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue>({
  user: null,
  login: async () => null,
  register: async () => null,
  logout: () => {},
})

function getSavedUser(): AuthUser | null {
  try { return JSON.parse(localStorage.getItem('pvcs-session') ?? 'null') } catch { return null }
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(getSavedUser)

  async function login(username: string, password: string): Promise<string | null> {
    try {
      const res = await fetch(`${API_URL}/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password }),
      })
      if (!res.ok) {
        const data = await res.json().catch(() => ({}))
        return (data as { detail?: string }).detail ?? 'Login failed. Please try again.'
      }
      const data = await res.json() as { user_id: string; name: string; username: string }
      const session: AuthUser = { userId: data.user_id, name: data.name, username: data.username }
      localStorage.setItem('pvcs-session', JSON.stringify(session))
      setUser(session)
      return null
    } catch {
      return 'Could not reach the server. Please try again.'
    }
  }

  async function register(name: string, username: string, password: string): Promise<string | null> {
    try {
      const res = await fetch(`${API_URL}/auth/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, username, password }),
      })
      if (!res.ok) {
        const data = await res.json().catch(() => ({}))
        return (data as { detail?: string }).detail ?? 'Registration failed. Please try again.'
      }
      const data = await res.json() as { user_id: string; name: string; username: string }
      const session: AuthUser = { userId: data.user_id, name: data.name, username: data.username }
      localStorage.setItem('pvcs-session', JSON.stringify(session))
      setUser(session)
      return null
    } catch {
      return 'Could not reach the server. Please try again.'
    }
  }

  function logout() {
    localStorage.removeItem('pvcs-session')
    setUser(null)
  }

  return (
    <AuthContext.Provider value={{ user, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  return useContext(AuthContext)
}
