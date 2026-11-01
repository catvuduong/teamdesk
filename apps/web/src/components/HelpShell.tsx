import type { ReactNode } from 'react'
import { useAuth } from '../auth/AuthContext'
import { AppShell } from './AppShell'
import { Link } from './Link'
import { ThemeToggle } from './ThemeToggle'

/** Help centre pages: the signed-in app shell for staff and customers, a slim public header for visitors. */
export function HelpShell({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  if (user) return <AppShell>{children}</AppShell>

  return (
    <div className="shell guest">
      <a href="#main" className="skip-link">
        Skip to content
      </a>
      <header className="topbar">
        <Link to="/kb" className="brand">
          <span className="brand-mark" aria-hidden="true">
            TD
          </span>
          <span>TeamDesk help</span>
        </Link>
        <div className="topbar-end">
          <ThemeToggle />
          <Link to="/login" className="button secondary">
            Sign in
          </Link>
        </div>
      </header>
      <main id="main" className="page">
        {children}
      </main>
    </div>
  )
}
