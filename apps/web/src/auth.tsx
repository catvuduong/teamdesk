import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { ApiError, UNAUTHORIZED_EVENT, api, readToken, writeToken } from './api'
import { AuthContext, type AuthState, type AuthStatus } from './auth-context'
import type { User } from './types'

/** Holds the signed-in user. A stored token is checked with GET /me on load; an expired one drops back to sign-in. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('booting')
  const [user, setUser] = useState<User | null>(null)

  useEffect(() => {
    const token = readToken()
    if (!token) {
      setStatus('anonymous')
      return
    }

    let cancelled = false
    api
      .me()
      .then((me) => {
        if (cancelled) return
        setUser(me)
        setStatus('ready')
      })
      .catch((error: unknown) => {
        if (cancelled) return
        if (error instanceof ApiError && error.status === 401) writeToken(null)
        setUser(null)
        setStatus('anonymous')
      })
    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    const onUnauthorized = () => {
      setUser(null)
      setStatus('anonymous')
    }
    window.addEventListener(UNAUTHORIZED_EVENT, onUnauthorized)
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, onUnauthorized)
  }, [])

  const signIn = useCallback((token: string, signedIn: User) => {
    writeToken(token)
    setUser(signedIn)
    setStatus('ready')
  }, [])

  const signOut = useCallback(() => {
    writeToken(null)
    setUser(null)
    setStatus('anonymous')
  }, [])

  const value = useMemo<AuthState>(() => ({ status, user, signIn, signOut }), [status, user, signIn, signOut])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
