import type { NewTicket, PublicUser, Role, SlaStats, Stats, TicketDetail, TicketPage, TicketPatch } from './types'
import { readToken } from './session'

export const API_URL: string = import.meta.env.VITE_API_URL ?? 'http://localhost:3001'

/** An error from the API. `errors` holds per-field messages for validation failures. */
export class ApiError extends Error {
  status: number
  errors: Record<string, string>

  constructor(status: number, message: string, errors: Record<string, string> = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.errors = errors
  }
}

interface AuthResponse {
  token: string
  user: PublicUser
}

interface Health {
  status: string
  db: string
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  const token = readToken()
  if (token) headers.Authorization = `Bearer ${token}`

  let response: Response
  try {
    response = await fetch(`${API_URL}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, 'Cannot reach the server. Check your connection and try again.')
  }

  const data: unknown = await response.json().catch(() => ({}))
  if (!response.ok) {
    const payload = data as { error?: string; errors?: Record<string, string> }
    throw new ApiError(response.status, payload.error ?? `Request failed (${response.status})`, payload.errors ?? {})
  }
  return data as T
}

export const api = {
  health: async (): Promise<string> => {
    const response = await fetch(`${API_URL}/health`)
    const data = (await response.json()) as Health
    return `api ${data.status} / db ${data.db}`
  },

  login: (email: string, password: string) =>
    request<AuthResponse>('POST', '/auth/login', { email, password }),

  register: (name: string, email: string, password: string) =>
    request<AuthResponse>('POST', '/auth/register', { name, email, password }),

  me: () => request<PublicUser>('GET', '/me'),

  listTickets: (params: URLSearchParams) => request<TicketPage>('GET', `/tickets?${params.toString()}`),

  getTicket: (id: number) => request<TicketDetail>('GET', `/tickets/${id}`),

  createTicket: (ticket: NewTicket) => request<TicketDetail>('POST', '/tickets', ticket),

  updateTicket: (id: number, patch: TicketPatch) => request<TicketDetail>('PATCH', `/tickets/${id}`, patch),

  addComment: (id: number, body: string, internal: boolean) =>
    request<unknown>('POST', `/tickets/${id}/comments`, { body, internal }),

  listUsers: (role?: Role) => request<PublicUser[]>('GET', role ? `/users?role=${role}` : '/users'),

  updateUserRole: (id: number, role: Role) => request<PublicUser>('PATCH', `/users/${id}`, { role }),

  stats: () => request<Stats>('GET', '/stats'),

  slaStats: () => request<SlaStats>('GET', '/stats/sla'),

  /** Downloads the CSV for the given list filters. A plain link cannot send the bearer token, so the file is fetched first. */
  exportTicketsCsv: async (params: URLSearchParams): Promise<Blob> => {
    const token = readToken()
    const headers: Record<string, string> = {}
    if (token) headers.Authorization = `Bearer ${token}`

    let response: Response
    try {
      response = await fetch(`${API_URL}/tickets/export.csv?${params.toString()}`, { headers })
    } catch {
      throw new ApiError(0, 'Cannot reach the server. Check your connection and try again.')
    }
    if (!response.ok) {
      const payload = (await response.json().catch(() => ({}))) as { error?: string }
      throw new ApiError(response.status, payload.error ?? `Export failed (${response.status})`)
    }
    return response.blob()
  },
}
