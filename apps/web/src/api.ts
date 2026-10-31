import type {
  AuthResponse,
  CommentView,
  Priority,
  Role,
  Stats,
  Status,
  TicketDetail,
  TicketPage,
  TicketQuery,
  User,
} from './types'

export const API_URL: string = import.meta.env.VITE_API_URL ?? 'http://localhost:3001'

const TOKEN_KEY = 'teamdesk.token'

/** Dispatched on window when the API rejects a request that carried a token (expired or deleted account). */
export const UNAUTHORIZED_EVENT = 'teamdesk:unauthorized'

/** An API failure with its HTTP status and, for validation errors, the per-field messages. */
export class ApiError extends Error {
  status: number
  fields: Record<string, string>

  constructor(status: number, message: string, fields: Record<string, string> = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.fields = fields
  }
}

export function readToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY)
  } catch {
    return null
  }
}

export function writeToken(token: string | null): void {
  try {
    if (token) localStorage.setItem(TOKEN_KEY, token)
    else localStorage.removeItem(TOKEN_KEY)
  } catch {
    // Storage is unavailable (private window, blocked cookies): the session simply lasts until reload
  }
}

export function messageOf(error: unknown): string {
  if (error instanceof ApiError) return error.message
  if (error instanceof Error) return error.message
  return 'Something went wrong'
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const token = readToken()
  const headers: Record<string, string> = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers.Authorization = `Bearer ${token}`

  let response: Response
  try {
    response = await fetch(`${API_URL}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, 'Cannot reach the TeamDesk API. Check that it is running.')
  }

  if (response.status === 401 && token) {
    writeToken(null)
    window.dispatchEvent(new Event(UNAUTHORIZED_EVENT))
  }

  const payload: unknown = await response.json().catch(() => null)
  if (!response.ok) {
    const data = (payload ?? {}) as { error?: string; errors?: Record<string, string> }
    throw new ApiError(response.status, data.error ?? `Request failed (${response.status})`, data.errors ?? {})
  }
  return payload as T
}

function query(params: Record<string, string | number | boolean | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === '') continue
    search.set(key, String(value))
  }
  const text = search.toString()
  return text ? `?${text}` : ''
}

export const api = {
  health: () => request<{ status: string; db: string }>('GET', '/health'),

  login: (email: string, password: string) =>
    request<AuthResponse>('POST', '/auth/login', { email, password }),

  register: (name: string, email: string, password: string) =>
    request<AuthResponse>('POST', '/auth/register', { name, email, password }),

  me: () => request<User>('GET', '/me'),

  listTickets: (filter: TicketQuery) =>
    request<TicketPage>(
      'GET',
      `/tickets${query({
        status: filter.status,
        priority: filter.priority,
        category: filter.category,
        assignee: filter.assignee,
        q: filter.q,
        mine: filter.mine ? true : undefined,
        sort: filter.sort,
        page: filter.page,
        pageSize: filter.pageSize,
      })}`,
    ),

  createTicket: (input: { title: string; description: string; category: string; priority: Priority }) =>
    request<TicketDetail>('POST', '/tickets', input),

  getTicket: (id: number) => request<TicketDetail>('GET', `/tickets/${id}`),

  updateTicket: (id: number, patch: { status?: Status; priority?: Priority; assigneeId?: number | null }) =>
    request<TicketDetail>('PATCH', `/tickets/${id}`, patch),

  addComment: (id: number, body: string, internal: boolean) =>
    request<CommentView>('POST', `/tickets/${id}/comments`, { body, internal }),

  listUsers: (role?: Role) => request<User[]>('GET', `/users${query({ role })}`),

  updateUserRole: (id: number, role: Role) => request<User>('PATCH', `/users/${id}`, { role }),

  stats: () => request<Stats>('GET', '/stats'),
}
