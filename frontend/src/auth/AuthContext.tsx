import { createContext, useCallback, useContext, useEffect, useMemo, useReducer, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { authApi } from '../api/endpoints'
import { UNAUTHORIZED_EVENT } from '../api/client'
import type { CurrentUser } from '../api/types'

type AuthState =
  | { status: 'loading'; user: null }
  | { status: 'authenticated'; user: CurrentUser }
  | { status: 'anonymous'; user: null }

type AuthAction = { type: 'signedIn'; user: CurrentUser } | { type: 'signedOut' }

// useReducer keeps auth transitions explicit: loading -> authenticated | anonymous.
function authReducer(_state: AuthState, action: AuthAction): AuthState {
  switch (action.type) {
    case 'signedIn': return { status: 'authenticated', user: action.user }
    case 'signedOut': return { status: 'anonymous', user: null }
  }
}

interface AuthContextValue {
  state: AuthState
  user: CurrentUser | null
  isAdmin: boolean
  signIn: (user: CurrentUser) => void
  signOut: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(authReducer, { status: 'loading', user: null })
  const queryClient = useQueryClient()

  // Bootstrap: obtain a CSRF token, then restore the session from the auth cookie.
  useEffect(() => {
    let cancelled = false
    authApi.csrf()
      .then(() => authApi.me())
      .then((user) => { if (!cancelled) dispatch({ type: 'signedIn', user }) })
      .catch(() => { if (!cancelled) dispatch({ type: 'signedOut' }) })
    return () => { cancelled = true }
  }, [])

  // Any 401 from the API (expired cookie, deactivated account) ends the session.
  useEffect(() => {
    const onUnauthorized = () => { queryClient.clear(); dispatch({ type: 'signedOut' }) }
    window.addEventListener(UNAUTHORIZED_EVENT, onUnauthorized)
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, onUnauthorized)
  }, [queryClient])

  const signIn = useCallback((user: CurrentUser) => {
    queryClient.clear() // never show cached data from a previous user
    dispatch({ type: 'signedIn', user })
  }, [queryClient])

  const signOut = useCallback(async () => {
    try { await authApi.logout() } finally {
      queryClient.clear()
      dispatch({ type: 'signedOut' })
    }
  }, [queryClient])

  const value = useMemo<AuthContextValue>(() => ({
    state, user: state.user, isAdmin: state.user?.role === 'Admin', signIn, signOut,
  }), [state, signIn, signOut])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
