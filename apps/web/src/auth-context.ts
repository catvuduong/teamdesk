import { createContext, useContext } from 'react'
import type { User } from './types'

export type AuthStatus = 'booting' | 'anonymous' | 'ready'

export interface AuthState {
  status: AuthStatus
  user: User | null
  signIn: (token: string, user: User) => void
  signOut: () => void
}

export const AuthContext = createContext<AuthState | null>(null)

export function useAuth(): AuthState {
  const value = useContext(AuthContext)
  if (!value) throw new Error('useAuth must be used inside AuthProvider')
  return value
}

export function isStaff(user: User | null): boolean {
  return user !== null && (user.role === 'agent' || user.role === 'admin')
}
