import { useEffect } from 'react'
import { AuthProvider } from './auth/AuthProvider'
import { useAuth } from './auth/AuthContext'
import { AppShell } from './components/AppShell'
import { EmptyState, Loading } from './components/States'
import { DashboardPage } from './pages/DashboardPage'
import { LoginPage } from './pages/LoginPage'
import { NewTicketPage } from './pages/NewTicketPage'
import { TicketDetailPage } from './pages/TicketDetailPage'
import { TicketsPage } from './pages/TicketsPage'
import { UsersPage } from './pages/UsersPage'
import { Link } from './components/Link'
import { navigate, redirectFor, useLocation } from './lib/router'

export default function App() {
  return (
    <AuthProvider>
      <Routes />
    </AuthProvider>
  )
}

function Routes() {
  const { loading, user } = useAuth()
  const { pathname } = useLocation()
  const target = loading ? null : redirectFor(user, pathname)

  useEffect(() => {
    if (target) navigate(target, { replace: true })
  }, [target])

  if (loading) return <Loading label="Starting TeamDesk" />
  if (target) return <Loading label="Redirecting" />
  if (!user) return <LoginPage />

  return <Page pathname={pathname} />
}

function Page({ pathname }: { pathname: string }) {
  if (pathname === '/dashboard') return <DashboardPage />
  if (pathname === '/users') return <UsersPage />
  if (pathname === '/tickets') return <TicketsPage />
  if (pathname === '/tickets/new') return <NewTicketPage />

  const ticket = /^\/tickets\/(\d+)$/.exec(pathname)
  if (ticket) return <TicketDetailPage key={ticket[1]} id={Number(ticket[1])} />

  return (
    <AppShell>
      <EmptyState title="Page not found">
        <Link to="/" className="button secondary">
          Go to the start page
        </Link>
      </EmptyState>
    </AppShell>
  )
}
