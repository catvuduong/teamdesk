import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api } from '../lib/api'
import { readToken, writeToken } from '../lib/session'
import type { PublicUser } from '../lib/types'
import { AuthContext, type AuthState } from './AuthContext'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<PublicUser | null>(null)
  const [loading, setLoading] = useState(() => readToken() !== null)

  // A stored token is only trusted after the API confirms it
  useEffect(() => {
    if (!readToken()) return
    let cancelled = false
    api
      .me()
      .then((me) => {
        if (!cancelled) setUser(me)
      })
      .catch(() => writeToken(null))
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [])

  const signIn = useCallback(async (email: string, password: string) => {
    const { token, user: signedIn } = await api.login(email, password)
    writeToken(token)
    setUser(signedIn)
    return signedIn
  }, [])

  const register = useCallback(async (name: string, email: string, password: string) => {
    const { token, user: created } = await api.register(name, email, password)
    writeToken(token)
    setUser(created)
    return created
  }, [])

  const signOut = useCallback(() => {
    writeToken(null)
    setUser(null)
  }, [])

  const value = useMemo<AuthState>(
    () => ({ loading, user, signIn, register, signOut }),
    [loading, user, signIn, register, signOut],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
