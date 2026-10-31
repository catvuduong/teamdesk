import { useEffect, useState } from 'react'
import { SLA_STATE_LABEL } from '../lib/constants'
import { formatSlaRemaining } from '../lib/format'
import type { SlaStatus } from '../lib/types'

const NEXT_DUE_LABEL = { first_response: 'First response', resolution: 'Resolution' } as const

/** The current time, refreshed every minute, so countdowns stay roughly right without a timer per render. */
function useNow(): number {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 60_000)
    return () => window.clearInterval(timer)
  }, [])
  return now
}

/** The SLA state as a badge, with a countdown to the next open deadline. Staff-only in the UI. */
export function SlaBadge({ sla, testId }: { sla: SlaStatus; testId?: string }) {
  const now = useNow()
  return (
    <span className="sla" data-testid={testId} data-state={sla.state}>
      <span className={`badge sla-${sla.state}`}>{SLA_STATE_LABEL[sla.state]}</span>
      {sla.nextDue && sla.dueAt && (
        <span className="sla-countdown" title={`${NEXT_DUE_LABEL[sla.nextDue]} deadline`}>
          {formatSlaRemaining(sla.dueAt, now)}
        </span>
      )}
    </span>
  )
}
