import type { ReactNode } from 'react'
import { useAuth } from '../auth/AuthContext'
import { ROLE_LABEL } from '../lib/constants'
import { initials } from '../lib/format'
import { useLocation } from '../lib/router'
import { Link } from './Link'
import { ThemeToggle } from './ThemeToggle'

interface NavItem {
  to: string
  label: string
  match: (pathname: string) => boolean
  staffOnly?: boolean
}

const NAV: NavItem[] = [
  { to: '/dashboard', label: 'Dashboard', match: (p) => p === '/dashboard', staffOnly: true },
  { to: '/tickets', label: 'Tickets', match: (p) => p === '/tickets' || (p.startsWith('/tickets/') && p !== '/tickets/new') },
  { to: '/tickets/new', label: 'New ticket', match: (p) => p === '/tickets/new' },
  { to: '/kb', label: 'Help center', match: (p) => p === '/kb' || p.startsWith('/kb/') },
  { to: '/sla', label: 'SLA', match: (p) => p === '/sla', staffOnly: true },
  { to: '/users', label: 'Users', match: (p) => p === '/users', staffOnly: true },
]

/** Sidebar navigation on desktop; on phones the same items sit in a scrolling row under the brand. */
export function AppShell({ children }: { children: ReactNode }) {
  const { user, signOut } = useAuth()
  const { pathname } = useLocation()
  if (!user) return null

  const isStaff = user.role !== 'customer'
  const items = NAV.filter((item) => isStaff || !item.staffOnly)

  return (
    <div className="shell">
      <a href="#main" className="skip-link">
        Skip to content
      </a>
      <header className="sidebar">
        <div className="sidebar-head">
          <Link to={isStaff ? '/dashboard' : '/tickets'} className="brand">
            <span className="brand-mark" aria-hidden="true">
              TD
            </span>
            <span>TeamDesk</span>
          </Link>
        </div>

        <nav aria-label="Main" className="nav">
          {items.map((item) => (
            <Link key={item.to} to={item.to} className={item.match(pathname) ? 'nav-link active' : 'nav-link'} ariaCurrent={item.match(pathname) ? 'page' : undefined}>
              {item.label}
            </Link>
          ))}
        </nav>

        <div className="sidebar-foot">
          <div className="user-chip">
            <span className="avatar" aria-hidden="true">
              {initials(user.name)}
            </span>
            <span className="user-meta">
              <span className="user-name">{user.name}</span>
              <span className="user-role">{ROLE_LABEL[user.role]}</span>
            </span>
          </div>
          <div className="sidebar-actions">
            <ThemeToggle />
            <button type="button" className="button ghost sign-out" onClick={signOut}>
              Sign out
            </button>
          </div>
        </div>
      </header>

      <div className="shell-main">
        <main id="main" className="page">
          {children}
        </main>
      </div>
    </div>
  )
}
