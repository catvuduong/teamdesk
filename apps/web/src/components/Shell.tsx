import type { ReactNode } from 'react'
import { useAuth, isStaff } from '../auth-context'
import { navigate, usePathname, resolveRoute } from '../router'
import { Link } from './Link'
import { RoleBadge } from './Badges'
import { ThemeToggle } from './ThemeToggle'

interface NavItem {
  to: string
  label: string
  staffOnly: boolean
  routes: string[]
}

const NAV: NavItem[] = [
  { to: '/dashboard', label: 'Dashboard', staffOnly: true, routes: ['dashboard'] },
  { to: '/tickets', label: 'Tickets', staffOnly: false, routes: ['tickets', 'ticket'] },
  { to: '/tickets/new', label: 'New ticket', staffOnly: false, routes: ['new-ticket'] },
  { to: '/users', label: 'Users', staffOnly: true, routes: ['users'] },
]

/** Header and navigation. Customers only get the links they can use; the API enforces the same rules. */
export function Shell({ children }: { children: ReactNode }) {
  const { status, user, signOut } = useAuth()
  const pathname = usePathname()
  const route = resolveRoute(pathname)
  const staff = isStaff(user)

  return (
    <div className="app">
      <header className="topbar">
        <div className="topbar-inner">
          <Link to="/" className="brand">
            <span className="brand-mark" aria-hidden="true">
              TD
            </span>
            <span>TeamDesk</span>
          </Link>

          {status === 'ready' && user && (
            <nav className="nav" aria-label="Main">
              {NAV.filter((item) => staff || !item.staffOnly).map((item) => {
                const active = item.routes.includes(route.name)
                return (
                  <Link key={item.to} to={item.to} className={active ? 'nav-link active' : 'nav-link'} ariaCurrent={active ? 'page' : undefined}>
                    {item.label}
                  </Link>
                )
              })}
            </nav>
          )}

          <div className="topbar-actions">
            {status === 'ready' && user && (
              <div className="whoami">
                <span className="whoami-name">{user.name}</span>
                <RoleBadge role={user.role} />
              </div>
            )}
            <ThemeToggle />
            {status === 'ready' && (
              <button
                type="button"
                className="button ghost"
                onClick={() => {
                  signOut()
                  navigate('/', { replace: true })
                }}
              >
                Sign out
              </button>
            )}
          </div>
        </div>
      </header>
      <main className="content">{children}</main>
    </div>
  )
}
