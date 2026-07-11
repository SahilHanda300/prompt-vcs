import { create } from 'zustand'
import type { ChatMessage } from '../types'

interface SiteSession {
  sessionId: string
  messages: ChatMessage[]
}

interface SiteStore {
  userName: string | null
  sessions: Record<string, SiteSession>
  setUserName: (name: string) => void
  getSession: (key: string) => SiteSession
  addMessage: (key: string, message: ChatMessage) => void
  appendToLastMessage: (key: string, chunk: string) => void
  setLastMessageStreaming: (key: string, streaming: boolean) => void
  clearSession: (key: string) => void
  setMessages: (key: string, messages: ChatMessage[]) => void
}

export const useSiteStore = create<SiteStore>((set, get) => ({
  userName: null,
  sessions: {},

  setUserName(name) {
    set({ userName: name.trim() })
  },

  getSession(key) {
    const existing = get().sessions[key]
    if (existing) return existing
    const session: SiteSession = { sessionId: crypto.randomUUID(), messages: [] }
    set(state => ({ sessions: { ...state.sessions, [key]: session } }))
    return session
  },

  addMessage(key, message) {
    set(state => {
      const session = state.sessions[key] ?? { sessionId: crypto.randomUUID(), messages: [] }
      return {
        sessions: {
          ...state.sessions,
          [key]: { ...session, messages: [...session.messages, message] },
        },
      }
    })
  },

  appendToLastMessage(key, chunk) {
    set(state => {
      const session = state.sessions[key]
      if (!session) return state
      const messages = [...session.messages]
      const last = messages[messages.length - 1]
      if (!last) return state
      messages[messages.length - 1] = { ...last, content: last.content + chunk }
      return { sessions: { ...state.sessions, [key]: { ...session, messages } } }
    })
  },

  setLastMessageStreaming(key, streaming) {
    set(state => {
      const session = state.sessions[key]
      if (!session) return state
      const messages = [...session.messages]
      const last = messages[messages.length - 1]
      if (!last) return state
      messages[messages.length - 1] = { ...last, streaming }
      return { sessions: { ...state.sessions, [key]: { ...session, messages } } }
    })
  },

  clearSession(key) {
    set(state => ({
      sessions: {
        ...state.sessions,
        [key]: { sessionId: crypto.randomUUID(), messages: [] },
      },
    }))
  },

  setMessages(key, messages) {
    set(state => {
      const existing = state.sessions[key]
      return {
        sessions: {
          ...state.sessions,
          [key]: {
            sessionId: existing?.sessionId ?? crypto.randomUUID(),
            messages,
          },
        },
      }
    })
  },
}))
