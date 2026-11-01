import { useEffect, useMemo, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { AppShell } from '../components/AppShell'
import { PriorityBadge, StatusBadge } from '../components/Badges'
import { SlaBadge } from '../components/SlaBadge'
import { ErrorState, EmptyState, Loading, SkeletonTable } from '../components/States'
import { Link } from '../components/Link'
import { api } from '../lib/api'
import { CATEGORIES, PRIORITIES, PRIORITY_LABEL, SLA_STATE_LABEL, STATUSES, STATUS_LABEL } from '../lib/constants'
import { messageOf } from '../lib/errors'
import { timeAgo } from '../lib/format'
import { navigate, useLocation } from '../lib/router'
import type { PublicUser, TicketPage } from '../lib/types'

const PAGE_SIZE = 20

/** Filters that narrow the list. Paging is not one of them, so an export always covers every matching ticket. */
const FILTER_KEYS = ['q', 'status', 'priority', 'category', 'assignee', 'mine', 'sort', 'sla'] as const

/** Quick filters for the SLA states that need attention. */
const SLA_QUICK_FILTERS = ['at_risk', 'breached'] as const

const SORT_LABEL: Record<string, string> = {
  newest: 'Newest first',
  updated: 'Recently updated',
  priority: 'Priority, urgent first',
}

/** The list reads its filters from the URL, so a filtered view can be bookmarked and shared. */
export function TicketsPage() {
  const { user } = useAuth()
  const { search } = useLocation()
  const isStaff = user?.role !== 'customer'

  const params = useMemo(() => new URLSearchParams(search), [search])
  const query = useMemo(() => {
    const next = new URLSearchParams()
    for (const key of [...FILTER_KEYS, 'page']) {
      const value = params.get(key)
      if (value) next.set(key, value)
    }
    next.set('pageSize', String(PAGE_SIZE))
    return next
  }, [params])
  const queryString = query.toString()

  const [data, setData] = useState<TicketPage | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [attempt, setAttempt] = useState(0)
  const [people, setPeople] = useState<PublicUser[]>([])
  const [searchText, setSearchText] = useState(params.get('q') ?? '')
  const [exporting, setExporting] = useState(false)
  const [exportError, setExportError] = useState<string | null>(null)

  async function exportCsv() {
    const filters = new URLSearchParams()
    for (const key of FILTER_KEYS) {
      const value = params.get(key)
      if (value) filters.set(key, value)
    }
    setExporting(true)
    setExportError(null)
    try {
      const blob = await api.exportTicketsCsv(filters)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = 'tickets.csv'
      link.click()
      window.setTimeout(() => URL.revokeObjectURL(url), 1000)
    } catch (err) {
      setExportError(messageOf(err))
    } finally {
      setExporting(false)
    }
  }

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)
    api
      .listTickets(new URLSearchParams(queryString))
      .then((page) => {
        if (!cancelled) setData(page)
      })
      .catch((err) => {
        if (!cancelled) setError(messageOf(err))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [queryString, attempt])

  useEffect(() => {
    if (!isStaff) return
    api
      .listUsers()
      .then((all) => setPeople(all.filter((u) => u.role !== 'customer')))
      .catch(() => setPeople([]))
  }, [isStaff])

  // Search waits for a short pause in typing before it touches the URL
  useEffect(() => {
    const timer = window.setTimeout(() => {
      const trimmed = searchText.trim()
      if ((params.get('q') ?? '') !== trimmed) setFilter('q', trimmed)
    }, 300)
    return () => window.clearTimeout(timer)
  }, [searchText])

  function setFilter(key: string, value: string) {
    const next = new URLSearchParams(search)
    if (value) next.set(key, value)
    else next.delete(key)
    if (key !== 'page') next.delete('page')
    const qs = next.toString()
    navigate(qs ? `/tickets?${qs}` : '/tickets', { replace: true })
  }

  function clearFilters() {
    setSearchText('')
    navigate('/tickets', { replace: true })
  }

  const page = Number(params.get('page') ?? '1') || 1
  const totalPages = data ? Math.max(1, Math.ceil(data.total / PAGE_SIZE)) : 1
  const filtersActive = ['q', 'status', 'priority', 'category', 'assignee', 'mine', 'sla'].some((key) => params.get(key))

  return (
    <AppShell>
      <header className="page-head">
        <div>
          <h1>Tickets</h1>
          <p className="muted">{data ? `${data.total} ${data.total === 1 ? 'ticket' : 'tickets'}` : 'Loading…'}</p>
        </div>
        <div className="row">
          {isStaff && (
            <button type="button" className="button secondary" onClick={exportCsv} disabled={exporting}>
              {exporting ? 'Exporting…' : 'Export CSV'}
            </button>
          )}
          <Link to="/tickets/new" className="button primary">
            New ticket
          </Link>
        </div>
      </header>
      {exportError && (
        <p role="alert" className="form-error">
          {exportError}
        </p>
      )}

      {isStaff && (
        <div className="chip-row" role="group" aria-label="SLA quick filters">
          {SLA_QUICK_FILTERS.map((sla) => {
            const active = params.get('sla') === sla
            return (
              <button
                key={sla}
                type="button"
                className={active ? 'chip active' : 'chip'}
                aria-pressed={active}
                onClick={() => setFilter('sla', active ? '' : sla)}
              >
                {SLA_STATE_LABEL[sla]}
              </button>
            )
          })}
        </div>
      )}

      <form role="search" className="filter-bar" onSubmit={(event) => event.preventDefault()}>
        <label className="field grow">
          <span className="visually-hidden">Search tickets</span>
          <input
            type="search"
            placeholder="Search title or description"
            value={searchText}
            onChange={(event) => setSearchText(event.target.value)}
          />
        </label>

        <label className="field">
          <span className="visually-hidden">Status</span>
          <select value={params.get('status') ?? ''} onChange={(e) => setFilter('status', e.target.value)}>
            <option value="">All statuses</option>
            {STATUSES.map((s) => (
              <option key={s} value={s}>
                {STATUS_LABEL[s]}
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span className="visually-hidden">Priority</span>
          <select value={params.get('priority') ?? ''} onChange={(e) => setFilter('priority', e.target.value)}>
            <option value="">All priorities</option>
            {PRIORITIES.map((p) => (
              <option key={p} value={p}>
                {PRIORITY_LABEL[p]}
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span className="visually-hidden">Category</span>
          <select value={params.get('category') ?? ''} onChange={(e) => setFilter('category', e.target.value)}>
            <option value="">All categories</option>
            {CATEGORIES.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        </label>

        {isStaff && (
          <label className="field">
            <span className="visually-hidden">Assignee</span>
            <select value={params.get('assignee') ?? ''} onChange={(e) => setFilter('assignee', e.target.value)}>
              <option value="">Anyone</option>
              <option value="unassigned">Unassigned</option>
              {people.map((p) => (
                <option key={p.id} value={String(p.id)}>
                  {p.name}
                </option>
              ))}
            </select>
          </label>
        )}

        {isStaff && (
          <label className="check">
            <input
              type="checkbox"
              checked={params.get('mine') === 'true'}
              onChange={(e) => setFilter('mine', e.target.checked ? 'true' : '')}
            />
            Mine
          </label>
        )}

        <label className="field">
          <span className="visually-hidden">Sort</span>
          <select value={params.get('sort') ?? 'newest'} onChange={(e) => setFilter('sort', e.target.value)}>
            {Object.entries(SORT_LABEL).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>

        {filtersActive && (
          <button type="button" className="button ghost" onClick={clearFilters}>
            Clear filters
          </button>
        )}
      </form>

      <div className={loading && data ? 'is-refreshing' : undefined} aria-busy={loading}>
        {error && <ErrorState message={error} onRetry={() => setAttempt((n) => n + 1)} />}
        {!error && !data && (
          <div className="card table-card">
            <Loading label="Loading tickets">
              <SkeletonTable rows={6} />
            </Loading>
          </div>
        )}
        {data && data.items.length === 0 && (
          <EmptyState title="No tickets match these filters">
            {filtersActive ? (
              <button type="button" className="button secondary" onClick={clearFilters}>
                Clear filters
              </button>
            ) : (
              <Link to="/tickets/new" className="button primary">
                Create the first ticket
              </Link>
            )}
          </EmptyState>
        )}
        {data && data.items.length > 0 && (
          <div className="card table-card">
            <table className="data-table stack-table ticket-table">
              <caption className="visually-hidden">Tickets</caption>
              <thead>
                <tr>
                  <th scope="col">Ticket</th>
                  <th scope="col">Status</th>
                  <th scope="col">Priority</th>
                  {isStaff && <th scope="col">SLA</th>}
                  <th scope="col">Assignee</th>
                  <th scope="col">Updated</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((ticket) => (
                  <tr key={ticket.id} data-testid="ticket-row">
                    <td className="ticket-cell">
                      <Link to={`/tickets/${ticket.id}`} className="ticket-link">
                        <span className="ticket-number">#{ticket.number}</span>
                        {ticket.title}
                      </Link>
                      <span className="ticket-meta">
                        {ticket.category} · {ticket.requester.name}
                      </span>
                    </td>
                    <td data-label="Status">
                      <StatusBadge status={ticket.status} />
                    </td>
                    <td data-label="Priority">
                      <PriorityBadge priority={ticket.priority} />
                    </td>
                    {isStaff && (
                      <td data-label="SLA">
                        <SlaBadge sla={ticket.sla} testId="sla-badge" />
                      </td>
                    )}
                    <td data-label="Assignee">
                      {ticket.assignee ? (
                        <span className="assignee">{ticket.assignee.name}</span>
                      ) : (
                        <span className="assignee-empty">Unassigned</span>
                      )}
                    </td>
                    <td data-label="Updated" className="muted">
                      {timeAgo(ticket.updatedAt)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {data && data.total > PAGE_SIZE && (
        <nav className="pagination" aria-label="Pagination">
          <button
            type="button"
            className="button secondary"
            disabled={page <= 1}
            onClick={() => setFilter('page', String(page - 1))}
          >
            Previous
          </button>
          <span className="muted">
            Page {page} of {totalPages}
          </span>
          <button
            type="button"
            className="button secondary"
            disabled={page >= totalPages}
            onClick={() => setFilter('page', String(page + 1))}
          >
            Next
          </button>
        </nav>
      )}
    </AppShell>
  )
}
