import { useEffect, useState } from 'react'
import { AppShell } from '../components/AppShell'
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
        <div className="dashboard-grid">
          <section className="card span-2" aria-labelledby="breach-title">
            <h2 id="breach-title" className="card-title">
              Breach rate by priority
            </h2>
            <SlaBreachChart rows={rows} />
          </section>

          <section className="card" aria-labelledby="states-title">
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
