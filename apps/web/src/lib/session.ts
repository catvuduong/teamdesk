const TOKEN_KEY = 'teamdesk.token'

// Storage can be blocked (private windows, disabled site data), so every access is guarded
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
    // The session then lasts only for this page load
  }
}
