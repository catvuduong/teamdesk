import { createContext, useContext } from 'react'
import type { PublicUser } from '../lib/types'

export interface AuthState {
  /** True until the stored token (if any) has been checked against the API. */
  loading: boolean
  user: PublicUser | null
  signIn: (email: string, password: string) => Promise<PublicUser>
  register: (name: string, email: string, password: string) => Promise<PublicUser>
  signOut: () => void
}

export const AuthContext = createContext<AuthState | null>(null)

export function useAuth(): AuthState {
  const value = useContext(AuthContext)
  if (!value) throw new Error('useAuth must be used inside AuthProvider')
  return value
}
