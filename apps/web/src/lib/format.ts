/** Times are UTC from the API; they are shown in the viewer's local time. */
export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

export function timeAgo(iso: string, now: number = Date.now()): string {
  const seconds = Math.round((now - new Date(iso).getTime()) / 1000)
  if (seconds < 60) return 'just now'
  const minutes = Math.round(seconds / 60)
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.round(minutes / 60)
  if (hours < 48) return `${hours}h ago`
  return `${Math.round(hours / 24)}d ago`
}

/** "2h 14m left" until the deadline, or "overdue by 3h" once it has passed. */
export function formatSlaRemaining(dueIso: string, now: number = Date.now()): string {
  const due = new Date(dueIso).getTime()
  const minutes = Math.round(Math.abs(due - now) / 60000)
  const span = minutes < 60 ? `${minutes}m` : `${Math.floor(minutes / 60)}h ${String(minutes % 60).padStart(2, '0')}m`
  return due < now ? `overdue by ${span}` : `${span} left`
}

export function formatHours(hours: number | null): string {
  if (hours === null) return '—'
  return `${hours.toFixed(1)} h`
}

/** "2026-10-31" shown as "Sat 31" in UTC, so the chart axis matches the API's days. */
export function formatDayLabel(date: string): string {
  return new Date(`${date}T00:00:00Z`).toLocaleDateString(undefined, {
    weekday: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  })
}

export function formatDayLong(date: string): string {
  return new Date(`${date}T00:00:00Z`).toLocaleDateString(undefined, {
    dateStyle: 'medium',
    timeZone: 'UTC',
  })
}
