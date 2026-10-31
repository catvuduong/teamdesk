import { PRIORITIES, PRIORITY_LABEL } from '../lib/constants'
import type { Priority } from '../lib/types'
import { PriorityBadge } from './Badges'

/** Counts per priority as proportional bars. One series, so one colour. */
export function PriorityBreakdown({ counts }: { counts: Record<Priority, number> }) {
  const total = PRIORITIES.reduce((sum, p) => sum + counts[p], 0)

  return (
    <section className="card" aria-labelledby="priority-title">
      <h2 id="priority-title" className="card-title">
        Priority breakdown
      </h2>
      <p className="muted">{total} tickets in total</p>
      <ul className="bars">
        {PRIORITIES.map((priority) => {
          const count = counts[priority]
          const percent = total === 0 ? 0 : (count / total) * 100
          return (
            <li key={priority} className="bar-row">
              <span className="bar-label">
                <PriorityBadge priority={priority} />
              </span>
              <span className="bar-track" aria-hidden="true">
                <span className="bar-fill" style={{ width: `${percent}%` }} />
              </span>
              <span className="bar-value">
                <span className="visually-hidden">{PRIORITY_LABEL[priority]}: </span>
                {count}
              </span>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
