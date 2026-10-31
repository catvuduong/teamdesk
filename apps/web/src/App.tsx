import { useEffect } from 'react'
import { AuthProvider } from './auth/AuthProvider'
import { useAuth } from './auth/AuthContext'
import { AppShell } from './components/AppShell'
import { EmptyState, Loading } from './components/States'
import { ArticlePage } from './pages/ArticlePage'
import { DashboardPage } from './pages/DashboardPage'
import { KnowledgeBasePage } from './pages/KnowledgeBasePage'
import { LoginPage } from './pages/LoginPage'
import { NewTicketPage } from './pages/NewTicketPage'
import { SlaPage } from './pages/SlaPage'
import { TicketDetailPage } from './pages/TicketDetailPage'
import { TicketsPage } from './pages/TicketsPage'
import { UsersPage } from './pages/UsersPage'
import { HelpShell } from './components/HelpShell'
import { Link } from './components/Link'
import { isHelpPath, navigate, redirectFor, useLocation } from './lib/router'

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
  if (!user && !isHelpPath(pathname)) return <LoginPage />

  return <Page pathname={pathname} />
}

function Page({ pathname }: { pathname: string }) {
  if (isHelpPath(pathname)) return <HelpPage pathname={pathname} />

  if (pathname === '/dashboard') return <DashboardPage />
  if (pathname === '/users') return <UsersPage />
  if (pathname === '/sla') return <SlaPage />
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

function HelpPage({ pathname }: { pathname: string }) {
  const article = /^\/kb\/([^/]+)$/.exec(pathname)
  if (article) return <ArticlePage key={article[1]} slug={decodeURIComponent(article[1])} />
  if (pathname === '/kb') return <KnowledgeBasePage />

  return (
    <HelpShell>
      <EmptyState title="Page not found">
        <Link to="/kb" className="button secondary">
          Browse the help centre
        </Link>
      </EmptyState>
    </HelpShell>
  )
}
