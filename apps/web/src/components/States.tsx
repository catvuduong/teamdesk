import type { ReactNode } from 'react'

/** A loading placeholder shaped like content. The label is read by screen readers; sighted users see the shapes. */
export function Loading({ label = 'Loading', children }: { label?: string; children?: ReactNode }) {
  return (
    <div className="state-loading" role="status" aria-live="polite">
      <span className="visually-hidden">{label}…</span>
      {children ?? <SkeletonLines />}
    </div>
  )
}

/** Stacked grey bars, for text-shaped content. */
export function SkeletonLines({ count = 3 }: { count?: number }) {
  return (
    <div className="skeleton-lines" aria-hidden="true">
      {Array.from({ length: count }, (_, index) => (
        <span key={index} className="skeleton" style={{ width: `${100 - index * 18}%` }} />
      ))}
    </div>
  )
}

/** Rows shaped like the tickets table, so the list does not jump when the data arrives. */
export function SkeletonTable({ rows = 6 }: { rows?: number }) {
  return (
    <div className="skeleton-table" aria-hidden="true">
      {Array.from({ length: rows }, (_, index) => (
        <div key={index} className="skeleton-row">
          <span className="skeleton" style={{ width: `${70 - (index % 3) * 10}%` }} />
          <span className="skeleton" style={{ width: '70%' }} />
          <span className="skeleton" style={{ width: '60%' }} />
          <span className="skeleton" style={{ width: '50%' }} />
        </div>
      ))}
    </div>
  )
}

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div className="state state-error" role="alert">
      <span>{message}</span>
      {onRetry && (
        <button type="button" className="button secondary small" onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  )
}

export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="state state-empty">
      <span className="empty-icon" aria-hidden="true">
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
          <path d="M3 13.5 5.6 5.8A2 2 0 0 1 7.5 4.5h9a2 2 0 0 1 1.9 1.3L21 13.5" />
          <path d="M3 13.5V18a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-4.5" />
          <path d="M3 13.5h4.5l1.5 2.5h6l1.5-2.5H21" />
        </svg>
      </span>
      <p className="empty-title">{title}</p>
      {children}
    </div>
  )
}
