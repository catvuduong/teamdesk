import { useEffect, useState } from 'react'
import { AppShell } from '../components/AppShell'
import { PriorityBadge } from '../components/Badges'
import { ErrorState, Loading } from '../components/States'
import { SlaBreachChart } from '../components/SlaBreachChart'
import { api } from '../lib/api'
import { messageOf } from '../lib/errors'
import type { PriorityBreach } from '../lib/types'

export function SlaPage() {
  const [rows, setRows] = useState<PriorityBreach[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let cancelled = false
    setError(null)
    api
      .slaStats()
      .then((stats) => {
        if (!cancelled) setRows(stats.byPriority)
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
          <h1>SLA</h1>
          <p className="muted">How often each priority has breached its response or resolution target.</p>
        </div>
      </header>

      {error && <ErrorState message={error} onRetry={() => setAttempt((n) => n + 1)} />}
      {!error && !rows && <Loading label="Loading SLA report" />}

      {rows && (
        <div className="dashboard-grid sla-grid">
          <section className="card sla-main" aria-labelledby="breach-title">
            <h2 id="breach-title" className="card-title">
              Breach rate by priority
            </h2>
            <SlaBreachChart rows={rows} />
          </section>

          <section className="card table-card sla-main" aria-labelledby="report-title">
            <h2 id="report-title" className="card-title table-title">
              Priority report
            </h2>
            <table className="data-table stack-table">
              <caption className="visually-hidden">Tickets and SLA breaches by priority</caption>
              <thead>
                <tr>
                  <th scope="col">Priority</th>
                  <th scope="col">Tickets</th>
                  <th scope="col">At risk</th>
                  <th scope="col">Breached</th>
                  <th scope="col">Breach rate</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.priority}>
                    <td data-label="Priority">
                      <PriorityBadge priority={row.priority} />
                    </td>
                    <td data-label="Tickets">{row.total}</td>
                    <td data-label="At risk">{row.atRisk}</td>
                    <td data-label="Breached">{row.breached}</td>
                    <td data-label="Breach rate">
                      {row.breachRate === null ? <span className="muted">No tickets</span> : `${row.breachRate.toFixed(1)}%`}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>

          <section className="card sla-aside" aria-labelledby="states-title">
            <h2 id="states-title" className="card-title">
              What the states mean
            </h2>
            <dl className="status-list">
              <div>
                <dt>On track</dt>
                <dd className="muted">Within target, or less than 75% of the time used.</dd>
              </div>
              <div>
                <dt>At risk</dt>
                <dd className="muted">More than 75% of the time used, with the target not yet met.</dd>
              </div>
              <div>
                <dt>Breached</dt>
                <dd className="muted">The target has passed without a reply or resolution in time.</dd>
              </div>
            </dl>
            <p className="muted">A ticket shows the worse of its first-response and resolution targets. Targets are set per priority in the API.</p>
          </section>
        </div>
      )}
    </AppShell>
  )
}
