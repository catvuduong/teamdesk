import { useEffect, useState, type FormEvent } from 'react'
import { ApiError, API_URL, api } from '../lib/api'
import { messageOf } from '../lib/errors'
import { DEMO_ACCOUNTS, DEMO_PASSWORD } from '../constants'
import { useAuth } from '../auth/AuthContext'
import { RoleBadge } from '../components/Badges'

export function LoginPage() {
  const { signIn, register } = useAuth()
  const [mode, setMode] = useState<'signin' | 'register'>('signin')
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [health, setHealth] = useState('checking…')

  useEffect(() => {
    let cancelled = false
    api
      .health()
      .then((status) => {
        if (!cancelled) setHealth(status)
      })
      .catch(() => {
        if (!cancelled) setHealth('api unreachable')
      })
    return () => {
      cancelled = true
    }
  }, [])

  const signInAs = async (account: string, accountEmail: string, accountPassword: string) => {
    setBusy(account)
    setError(null)
    try {
      await signIn(accountEmail, accountPassword)
    } catch (err) {
      setError(messageOf(err))
    } finally {
      setBusy(null)
    }
  }

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setBusy('form')
    setError(null)
    try {
      if (mode === 'signin') await signIn(email, password)
      else await register(name, email, password)
    } catch (err) {
      if (err instanceof ApiError && err.status === 400 && Object.keys(err.errors).length > 0) {
        setError(Object.values(err.errors).join(' '))
      } else {
        setError(messageOf(err))
      }
    } finally {
      setBusy(null)
    }
  }

  return (
    <div className="login">
      <div className="login-inner">
        <div className="login-brand">
          <span className="brand-mark brand-mark-lg" aria-hidden="true">
            TD
          </span>
          <span>TeamDesk</span>
        </div>

        <div className="login-grid">
          <section className="card login-card" aria-labelledby="login-title">
            <p className="eyebrow">Support for small teams</p>
            <h1 id="login-title">Sign in to TeamDesk</h1>
            <p className="muted">
              Track customer requests from first message to resolution. Pick a demo account to explore, or use your own.
            </p>

            <form className="stack" onSubmit={submit} noValidate>
              {mode === 'register' && (
                <div className="field">
                  <label htmlFor="login-name">Name</label>
                  <input id="login-name" autoComplete="name" value={name} onChange={(e) => setName(e.target.value)} />
                </div>
              )}
              <div className="field">
                <label htmlFor="login-email">Email</label>
                <input
                  id="login-email"
                  type="email"
                  autoComplete="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                />
              </div>
              <div className="field">
                <label htmlFor="login-password">Password</label>
                <input
                  id="login-password"
                  type="password"
                  autoComplete={mode === 'signin' ? 'current-password' : 'new-password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                />
              </div>
              <button type="submit" className="button primary" disabled={busy !== null}>
                {busy === 'form' ? 'Signing in…' : mode === 'signin' ? 'Sign in' : 'Create account'}
              </button>
            </form>

            <button
              type="button"
              className="button link"
              onClick={() => {
                setMode(mode === 'signin' ? 'register' : 'signin')
                setError(null)
              }}
            >
              {mode === 'signin' ? 'Create a customer account' : 'Back to sign in'}
            </button>

            <div className="error-slot" role="alert" aria-live="assertive">
              {error && <p className="form-error">{error}</p>}
            </div>
          </section>

          <section className="card demo-card" aria-labelledby="demo-title">
            <h2 id="demo-title">Demo accounts</h2>
            <p className="muted">
              Every demo account uses the password <code className="inline-code">{DEMO_PASSWORD}</code>. It only works
              against this demo data.
            </p>
            <ul className="demo-list">
              {DEMO_ACCOUNTS.map((account) => (
                <li key={account.email} className="demo-item">
                  <div>
                    <span className="demo-email">{account.email}</span>
                    <RoleBadge role={account.role} />
                  </div>
                  <button
                    type="button"
                    className="button primary small"
                    data-testid={account.testId}
                    disabled={busy !== null}
                    onClick={() => signInAs(account.testId, account.email, DEMO_PASSWORD)}
                  >
                    {busy === account.testId ? 'Signing in…' : account.label}
                  </button>
                </li>
              ))}
            </ul>
            <p className={health.startsWith('api ok') ? 'health health-ok' : 'health'} data-testid="health" title={API_URL}>
              {health}
            </p>
          </section>
        </div>
      </div>
    </div>
  )
}
