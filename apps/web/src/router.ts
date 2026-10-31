import { useSyncExternalStore } from 'react'

/** Minimal History API router: no library, just the current pathname and a navigate function. */
function subscribe(onChange: () => void): () => void {
  window.addEventListener('popstate', onChange)
  return () => window.removeEventListener('popstate', onChange)
}

function currentPath(): string {
  return window.location.pathname
}

export function usePathname(): string {
  return useSyncExternalStore(subscribe, currentPath, currentPath)
}

export function navigate(to: string, options: { replace?: boolean } = {}): void {
  if (to === window.location.pathname) return
  if (options.replace) window.history.replaceState(null, '', to)
  else window.history.pushState(null, '', to)
  window.dispatchEvent(new PopStateEvent('popstate'))
}

export type Route =
  | { name: 'home' }
  | { name: 'dashboard' }
  | { name: 'tickets' }
  | { name: 'new-ticket' }
  | { name: 'ticket'; id: number }
  | { name: 'users' }
  | { name: 'not-found' }

export function resolveRoute(pathname: string): Route {
  const path = pathname.replace(/\/+$/, '') || '/'
  if (path === '/') return { name: 'home' }
  if (path === '/dashboard') return { name: 'dashboard' }
  if (path === '/tickets') return { name: 'tickets' }
  if (path === '/tickets/new') return { name: 'new-ticket' }
  if (path === '/users') return { name: 'users' }

  const match = /^\/tickets\/(\d+)$/.exec(path)
  if (match) return { name: 'ticket', id: Number(match[1]) }

  return { name: 'not-found' }
}
