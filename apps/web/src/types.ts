export type Role = 'customer' | 'agent' | 'admin'
export type Status = 'open' | 'in_progress' | 'waiting' | 'resolved' | 'closed'
export type Priority = 'low' | 'medium' | 'high' | 'urgent'

export interface User {
  id: number
  name: string
  email: string
  role: Role
}

export interface PersonRef {
  id: number
  name: string
}

export interface AuthResponse {
  token: string
  user: User
}

export interface TicketSummary {
  id: number
  number: number
  title: string
  status: Status
  priority: Priority
  category: string
  requester: PersonRef
  assignee: PersonRef | null
  createdAt: string
  updatedAt: string
}

export interface TicketPage {
  items: TicketSummary[]
  total: number
  page: number
  pageSize: number
}

export interface CommentView {
  id: number
  body: string
  internal: boolean
  author: PersonRef
  authorRole: Role
  createdAt: string
}

export interface EventView {
  id: number
  type: string
  detail: string
  actor: PersonRef
  createdAt: string
}

export interface TicketDetail extends TicketSummary {
  description: string
  resolvedAt: string | null
  allowedStatuses: Status[]
  comments: CommentView[]
  events: EventView[]
}

export interface DayCount {
  date: string
  created: number
  resolved: number
}

export interface TopAgent {
  id: number
  name: string
  resolved: number
}

export interface Stats {
  open: number
  resolved: number
  resolvedToday: number
  byStatus: Record<Status, number>
  byPriority: Record<Priority, number>
  daily: DayCount[]
  avgFirstResponseHours: number | null
  avgResolutionHours: number | null
  topAgents: TopAgent[]
}

export interface TicketQuery {
  status?: string
  priority?: string
  category?: string
  assignee?: string
  q?: string
  mine?: boolean
  sort?: string
  page?: number
  pageSize?: number
}
