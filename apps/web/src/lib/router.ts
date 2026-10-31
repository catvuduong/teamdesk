import { useSyncExternalStore } from 'react'
import type { PublicUser } from './types'

const NAVIGATE_EVENT = 'teamdesk:navigate'

function subscribe(onChange: () => void) {
  window.addEventListener('popstate', onChange)
  window.addEventListener(NAVIGATE_EVENT, onChange)
  return () => {
    window.removeEventListener('popstate', onChange)
    window.removeEventListener(NAVIGATE_EVENT, onChange)
  }
}

export interface Location {
  pathname: string
  search: string
}

/** The current path and query string. Re-renders on navigation, including back and forward. */
export function useLocation(): Location {
  const href = useSyncExternalStore(subscribe, () => window.location.href, () => 'http://localhost/')
  const url = new URL(href)
  return { pathname: url.pathname, search: url.search }
}

export function navigate(to: string, options: { replace?: boolean } = {}): void {
  if (options.replace) window.history.replaceState(null, '', to)
  else window.history.pushState(null, '', to)
  window.dispatchEvent(new Event(NAVIGATE_EVENT))
}

/** The page a signed-in user lands on. Customers have no dashboard. */
export function homeFor(user: PublicUser): string {
  return user.role === 'customer' ? '/tickets' : '/dashboard'
}

/** Where to send the visitor instead of the requested path, or null when the path is fine. */
export function redirectFor(user: PublicUser | null, pathname: string): string | null {
  if (!user) return pathname === '/login' ? null : '/login'
  if (pathname === '/login' || pathname === '/') return homeFor(user)
  if (user.role === 'customer' && (pathname === '/dashboard' || pathname === '/users')) return '/tickets'
  return null
}
