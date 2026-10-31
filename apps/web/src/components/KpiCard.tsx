import type { ReactNode } from 'react'

interface KpiCardProps {
  testId: string
  label: string
  value: ReactNode
  hint: string
  accent: 'blue' | 'violet' | 'green' | 'orange'
}

export function KpiCard({ testId, label, value, hint, accent }: KpiCardProps) {
  return (
    <section className={`card kpi kpi-${accent}`} data-testid={testId} aria-label={label}>
      <p className="kpi-label">{label}</p>
      <p className="kpi-value">{value}</p>
      <p className="kpi-hint">{hint}</p>
    </section>
  )
}
