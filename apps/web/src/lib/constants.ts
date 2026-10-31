import type { Priority, Role, Status } from './types'

export const STATUSES: Status[] = ['open', 'in_progress', 'waiting', 'resolved', 'closed']
export const PRIORITIES: Priority[] = ['urgent', 'high', 'medium', 'low']
export const CATEGORIES = [
  'Login & access',
  'Billing',
  'Bug report',
  'Feature request',
  'How-to',
  'Account changes',
  'Integrations',
]

export const STATUS_LABEL: Record<Status, string> = {
  open: 'Open',
  in_progress: 'In progress',
  waiting: 'Waiting',
  resolved: 'Resolved',
  closed: 'Closed',
}

export const PRIORITY_LABEL: Record<Priority, string> = {
  urgent: 'Urgent',
  high: 'High',
  medium: 'Medium',
  low: 'Low',
}

export const ROLE_LABEL: Record<Role, string> = {
  customer: 'Customer',
  agent: 'Agent',
  admin: 'Admin',
}

export const EVENT_LABEL: Record<string, string> = {
  created: 'Created',
  status_changed: 'Status changed',
  priority_changed: 'Priority changed',
  assignee_changed: 'Assignee changed',
}

/** Demo accounts seeded by the API. The password is shown on the login page. */
export const DEMO_PASSWORD = 'teamdesk-demo'

export const DEMO_ACCOUNTS: { name: string; email: string; role: Role }[] = [
  { name: 'Dana Whitfield', email: 'admin@teamdesk.demo', role: 'admin' },
  { name: 'Priya Shah', email: 'agent@teamdesk.demo', role: 'agent' },
  { name: 'Marcus Lee', email: 'marcus.lee@teamdesk.demo', role: 'agent' },
  { name: 'Harper Quinn', email: 'customer@teamdesk.demo', role: 'customer' },
  { name: 'Leo Martins', email: 'leo.martins@teamdesk.demo', role: 'customer' },
]
