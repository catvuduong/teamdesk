export type Role = 'customer' | 'agent' | 'admin'
export type Status = 'open' | 'in_progress' | 'waiting' | 'resolved' | 'closed'
export type Priority = 'low' | 'medium' | 'high' | 'urgent'

export interface PublicUser {
  id: number
  name: string
  email: string
  role: Role
}

export interface PersonRef {
  id: number
  name: string
}

export type SlaState = 'on_track' | 'at_risk' | 'breached'

export interface SlaStatus {
  state: SlaState
  /** The open clock with the earliest deadline, or null when every clock has stopped */
  nextDue: 'first_response' | 'resolution' | null
  dueAt: string | null
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
  resolvedAt: string | null
  sla: SlaStatus
}

export interface PriorityBreach {
  priority: Priority
  total: number
  breached: number
  atRisk: number
  /** Percent of the priority's tickets that breached, or null when there are none */
  breachRate: number | null
}

export interface SlaStats {
  byPriority: PriorityBreach[]
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
  type: 'created' | 'status_changed' | 'priority_changed' | 'assignee_changed' | string
  detail: string
  actor: PersonRef
  createdAt: string
}

export interface RatingView {
  ticketId: number
  score: number
  comment: string | null
  createdAt: string
}

export interface TicketDetail extends TicketSummary {
  description: string
  resolvedAt: string | null
  allowedStatuses: Status[]
  comments: CommentView[]
  events: EventView[]
  rating: RatingView | null
}

export interface ArticleSummary {
  id: number
  slug: string
  title: string
  category: string
  excerpt: string
  views: number
  updatedAt: string
}

export interface ArticleDetail {
  id: number
  slug: string
  title: string
  body: string
  category: string
  published: boolean
  views: number
  createdAt: string
  updatedAt: string
}

export interface ScoreCount {
  score: number
  count: number
}

export interface CsatWeek {
  /** Monday of the week, yyyy-mm-dd, in UTC */
  weekStart: string
  count: number
  average: number | null
}

export interface CsatStats {
  count: number
  average: number | null
  distribution: ScoreCount[]
  trend: CsatWeek[]
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

export interface TicketPatch {
  status?: Status
  priority?: Priority
  assigneeId?: number | null
}

export interface NewTicket {
  title: string
  description: string
  category: string
  priority: Priority
}
