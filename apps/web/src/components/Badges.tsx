import type { Priority, Role, Status } from '../types'
import { labelOf } from '../constants'

export function StatusBadge({ status, testId }: { status: Status; testId?: string }) {
  return (
    <span className={`badge status-${status}`} data-testid={testId}>
      {labelOf(status)}
    </span>
  )
}

export function PriorityBadge({ priority }: { priority: Priority }) {
  return <span className={`badge priority-${priority}`}>{labelOf(priority)}</span>
}

export function RoleBadge({ role }: { role: Role }) {
  return <span className={`badge role-${role}`}>{labelOf(role)}</span>
}
