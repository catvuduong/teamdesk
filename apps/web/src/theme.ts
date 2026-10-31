export type Theme = 'light' | 'dark'

const THEME_KEY = 'teamdesk.theme'

/** The explicit choice if the user made one, otherwise null (CSS then follows prefers-color-scheme). */
export function storedTheme(): Theme | null {
  try {
    const value = localStorage.getItem(THEME_KEY)
    return value === 'light' || value === 'dark' ? value : null
  } catch {
    return null
  }
}

export function currentTheme(): Theme {
  const explicit = document.documentElement.dataset.theme
  if (explicit === 'light' || explicit === 'dark') return explicit
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

export function applyTheme(theme: Theme): void {
  document.documentElement.dataset.theme = theme
  try {
    localStorage.setItem(THEME_KEY, theme)
  } catch {
    // Without storage the choice still applies to this page view
  }
}

/** Applies the saved theme before the first paint so the page never flashes the wrong colours. */
export function applyStoredTheme(): void {
  const saved = storedTheme()
  if (saved) document.documentElement.dataset.theme = saved
}
