import { useState } from 'react'
import { applyTheme, currentTheme, type Theme } from '../theme'

export function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>(() => currentTheme())
  const next: Theme = theme === 'dark' ? 'light' : 'dark'

  const toggle = () => {
    applyTheme(next)
    setTheme(next)
  }

  return (
    <button type="button" className="button ghost theme-toggle" onClick={toggle} aria-label={`Switch to ${next} theme`}>
      <span aria-hidden="true">{theme === 'dark' ? '☀' : '☾'}</span>
      <span className="theme-label">{next === 'dark' ? 'Dark' : 'Light'}</span>
    </button>
  )
}
