import type { MouseEvent, ReactNode } from 'react'
import { navigate } from '../router'

interface LinkProps {
  to: string
  className?: string
  testId?: string
  children: ReactNode
  ariaCurrent?: 'page'
}

/** An anchor that navigates in-app on a plain left click and keeps normal link behaviour otherwise. */
export function Link({ to, className, testId, children, ariaCurrent }: LinkProps) {
  const onClick = (event: MouseEvent<HTMLAnchorElement>) => {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return
    event.preventDefault()
    navigate(to)
  }
  return (
    <a href={to} className={className} data-testid={testId} onClick={onClick} aria-current={ariaCurrent}>
      {children}
    </a>
  )
}
