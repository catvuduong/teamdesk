import type { Priority, Role, Status } from './types'

/** Demo accounts seeded by the API. The password is a documented demo value, shown on the login page. */
export const DEMO_PASSWORD = 'teamdesk-demo'

export const DEMO_ACCOUNTS: { testId: string; label: string; email: string; role: Role }[] = [
  { testId: 'login-demo-admin', label: 'Sign in as demo admin', email: 'admin@teamdesk.demo', role: 'admin' },
  { testId: 'login-demo-agent', label: 'Sign in as demo agent', email: 'agent@teamdesk.demo', role: 'agent' },
  { testId: 'login-demo-customer', label: 'Sign in as demo customer', email: 'customer@teamdesk.demo', role: 'customer' },
]

export const STATUSES: Status[] = ['open', 'in_progress', 'waiting', 'resolved', 'closed']
export const PRIORITIES: Priority[] = ['low', 'medium', 'high', 'urgent']
export const ROLES: Role[] = ['customer', 'agent', 'admin']

export const CATEGORIES = [
  'Login & access',
  'Billing',
  'Bug report',
  'Feature request',
  'How-to',
  'Integrations',
  'Account changes',
]

export const SORTS = [
  { value: 'newest', label: 'Newest first' },
  { value: 'updated', label: 'Recently updated' },
  { value: 'priority', label: 'Priority' },
]

export const PAGE_SIZE = 20

export function labelOf(value: string): string {
  const text = value.replace(/_/g, ' ')
  return text.charAt(0).toUpperCase() + text.slice(1)
}
