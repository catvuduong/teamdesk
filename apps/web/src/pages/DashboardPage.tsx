import { useEffect, useState } from 'react'
import { AppShell } from '../components/AppShell'
import { CsatPanel } from '../components/CsatPanel'
import { ErrorState, Loading, SkeletonLines } from '../components/States'
import { KpiCard } from '../components/KpiCard'
import { PriorityBreakdown } from '../components/PriorityBreakdown'
import { TicketsChart } from '../components/TicketsChart'
import { api } from '../lib/api'
import { STATUS_LABEL } from '../lib/constants'
import { formatHours, initials } from '../lib/format'
import { messageOf } from '../lib/errors'
import type { Stats } from '../lib/types'

export function DashboardPage() {
  const [stats, setStats] = useState<Stats | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let cancelled = false
    setError(null)
    api
      .stats()
      .then((data) => {
        if (!cancelled) setStats(data)
      })
      .catch((err) => {
        if (!cancelled) setError(messageOf(err))
      })
    return () => {
      cancelled = true
    }
  }, [attempt])

  return (
    <AppShell>
      <header className="page-head">
        <div>
          <h1>Dashboard</h1>
          <p className="muted">The queue at a glance. Days and times are UTC.</p>
        </div>
      </header>

      {error && <ErrorState message={error} onRetry={() => setAttempt((n) => n + 1)} />}
      {!error && !stats && (
        <Loading label="Loading the dashboard">
          <div className="kpi-row">
            {[0, 1, 2, 3].map((index) => (
              <section key={index} className="card">
                <SkeletonLines count={3} />
              </section>
            ))}
          </div>
        </Loading>
      )}

      {stats && (
        <div className="dashboard">
          <div className="kpi-row">
            <KpiCard testId="kpi-open" label="Open" value={stats.open} hint="Open, in progress or waiting" accent="blue" />
            <KpiCard
              testId="kpi-in-progress"
              label="In progress"
              value={stats.byStatus.in_progress}
              hint={`${stats.byStatus.waiting} waiting on the customer`}
              accent="violet"
            />
            <KpiCard
              testId="kpi-resolved-today"
              label="Resolved today"
              value={stats.resolvedToday}
              hint={`${stats.resolved} resolved or closed in total`}
              accent="green"
            />
            <KpiCard
              testId="kpi-response-time"
              label="Avg first response"
              value={formatHours(stats.avgFirstResponseHours)}
              hint={`Resolution takes ${formatHours(stats.avgResolutionHours)} on average`}
              accent="orange"
            />
          </div>

          <div className="dashboard-grid">
            <section className="card span-2">
              <TicketsChart days={stats.daily} />
            </section>
            <PriorityBreakdown counts={stats.byPriority} />

            <div className="span-2">
              <CsatPanel />
            </div>

            <section className="card" aria-labelledby="agents-title">
              <h2 id="agents-title" className="card-title">
                Top agents by resolved tickets
              </h2>
              {stats.topAgents.length === 0 ? (
                <p className="muted">No resolved tickets yet.</p>
              ) : (
                <ol className="leaderboard">
                  {stats.topAgents.map((agent) => {
                    const top = stats.topAgents[0].resolved
                    return (
                      <li key={agent.id}>
                        <span className="avatar small" aria-hidden="true">
                          {initials(agent.name)}
                        </span>
                        <span className="leader-name">{agent.name}</span>
                        <span className="bar-track" aria-hidden="true">
                          <span className="bar-fill" style={{ width: `${(agent.resolved / top) * 100}%` }} />
                        </span>
                        <span className="bar-value">{agent.resolved}</span>
                      </li>
                    )
                  })}
                </ol>
              )}
            </section>

            <section className="card span-3" aria-labelledby="status-title">
              <h2 id="status-title" className="card-title">
                Tickets by status
              </h2>
              <dl className="status-tiles">
                {(Object.keys(stats.byStatus) as (keyof Stats['byStatus'])[]).map((status) => (
                  <div key={status}>
                    <dt>{STATUS_LABEL[status]}</dt>
                    <dd>{stats.byStatus[status]}</dd>
                  </div>
                ))}
              </dl>
            </section>
          </div>
        </div>
      )}
    </AppShell>
  )
}
