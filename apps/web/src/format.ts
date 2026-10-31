const DATE_TIME = new Intl.DateTimeFormat('en-GB', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  timeZone: 'UTC',
})

const DAY = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', timeZone: 'UTC' })

/** A timestamp from the API (UTC) as "31 Oct 2026, 14:05 UTC". */
export function formatDateTime(iso: string): string {
  return `${DATE_TIME.format(new Date(iso))} UTC`
}

/** A "yyyy-MM-dd" day from the stats endpoint as "31 Oct". */
export function formatDay(day: string): string {
  return DAY.format(new Date(`${day}T00:00:00Z`))
}

/** Rough "5 min ago" / "3 h ago" / "2 d ago" for lists. */
export function timeAgo(iso: string, now: number = Date.now()): string {
  const minutes = Math.max(0, Math.round((now - new Date(iso).getTime()) / 60000))
  if (minutes < 60) return `${minutes} min ago`
  const hours = Math.round(minutes / 60)
  if (hours < 48) return `${hours} h ago`
  return `${Math.round(hours / 24)} d ago`
}

export function formatHours(hours: number | null): string {
  return hours === null ? '—' : `${hours.toFixed(1)} h`
}

export function formatPercent(part: number, whole: number): string {
  if (whole === 0) return '0%'
  return `${Math.round((part / whole) * 100)}%`
}
