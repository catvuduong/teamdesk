import { useEffect, useState, type FormEvent } from 'react'
import { AppShell } from '../components/AppShell'
import { Link } from '../components/Link'
import { ApiError, api } from '../lib/api'
import { CATEGORIES, PRIORITIES, PRIORITY_LABEL } from '../lib/constants'
import { messageOf } from '../lib/errors'
import { navigate } from '../lib/router'
import type { ArticleSummary, NewTicket } from '../lib/types'

/** Suggestions wait for a short pause in typing, and need a title long enough to match on. */
const SUGGEST_DELAY_MS = 300
const SUGGEST_MIN_LENGTH = 3

const EMPTY_FORM: NewTicket = {
  title: '',
  description: '',
  category: CATEGORIES[0],
  priority: 'medium',
}

export function NewTicketPage() {
  const [form, setForm] = useState<NewTicket>(EMPTY_FORM)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [suggestions, setSuggestions] = useState<ArticleSummary[]>([])

  useEffect(() => {
    const title = form.title.trim()
    if (title.length < SUGGEST_MIN_LENGTH) {
      setSuggestions([])
      return
    }
    let cancelled = false
    const timer = window.setTimeout(() => {
      api
        .suggestArticles(title)
        .then((items) => {
          if (!cancelled) setSuggestions(items)
        })
        .catch(() => {
          // Suggestions are a convenience: a failed lookup must never block the form
          if (!cancelled) setSuggestions([])
        })
    }, SUGGEST_DELAY_MS)
    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [form.title])

  function update<K extends keyof NewTicket>(key: K, value: NewTicket[K]) {
    setForm((current) => ({ ...current, [key]: value }))
    setErrors((current) => {
      if (!(key in current)) return current
      const { [key]: _resolved, ...rest } = current
      return rest
    })
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setErrors({})
    setFormError(null)
    try {
      const ticket = await api.createTicket(form)
      navigate(`/tickets/${ticket.id}`)
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.errors).length > 0) setErrors(err.errors)
      else setFormError(messageOf(err))
      setSubmitting(false)
    }
  }

  const hasErrors = Object.keys(errors).length > 0

  return (
    <AppShell>
      <header className="page-head">
        <div>
          <h1>New ticket</h1>
          <p className="muted">Tell us what happened. The more detail, the faster we can help.</p>
        </div>
      </header>

      <form data-testid="new-ticket-form" className="card form-card stack" onSubmit={onSubmit} noValidate>
        {hasErrors && (
          <p role="alert" className="form-error summary">
            Please fix the highlighted fields and try again.
          </p>
        )}
        {formError && (
          <p role="alert" className="form-error summary">
            {formError}
          </p>
        )}

        <div className="field">
          <label htmlFor="ticket-title">Title</label>
          <input
            id="ticket-title"
            value={form.title}
            aria-invalid={Boolean(errors.title)}
            aria-describedby={errors.title ? 'ticket-title-error' : undefined}
            onChange={(e) => update('title', e.target.value)}
            maxLength={300}
          />
          {errors.title && (
            <p id="ticket-title-error" className="field-error">
              {errors.title}
            </p>
          )}
          {suggestions.length > 0 && (
            <section className="suggestions" aria-labelledby="suggestions-title" aria-live="polite">
              <h2 id="suggestions-title" className="suggestions-title">
                These articles may already answer it
              </h2>
              <p className="muted">They open in a new tab, so your draft stays here.</p>
              <ul>
                {suggestions.map((article) => (
                  <li key={article.id}>
                    <a
                      href={`/kb/${article.slug}`}
                      target="_blank"
                      rel="noopener noreferrer"
                      data-testid="ticket-suggestion"
                    >
                      {article.title}
                    </a>
                    <span className="muted"> {article.excerpt}</span>
                  </li>
                ))}
              </ul>
            </section>
          )}
        </div>

        <div className="form-row">
          <div className="field">
            <label htmlFor="ticket-category">Category</label>
            <select
              id="ticket-category"
              value={form.category}
              aria-invalid={Boolean(errors.category)}
              onChange={(e) => update('category', e.target.value)}
            >
              {CATEGORIES.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
            {errors.category && <p className="field-error">{errors.category}</p>}
          </div>

          <div className="field">
            <label htmlFor="ticket-priority">Priority</label>
            <select
              id="ticket-priority"
              value={form.priority}
              aria-invalid={Boolean(errors.priority)}
              onChange={(e) => update('priority', e.target.value as NewTicket['priority'])}
            >
              {PRIORITIES.slice().reverse().map((p) => (
                <option key={p} value={p}>
                  {PRIORITY_LABEL[p]}
                </option>
              ))}
            </select>
            {errors.priority && <p className="field-error">{errors.priority}</p>}
          </div>
        </div>

        <div className="field">
          <label htmlFor="ticket-description">Description</label>
          <textarea
            id="ticket-description"
            rows={7}
            value={form.description}
            aria-invalid={Boolean(errors.description)}
            aria-describedby={errors.description ? 'ticket-description-error' : undefined}
            onChange={(e) => update('description', e.target.value)}
          />
          {errors.description && (
            <p id="ticket-description-error" className="field-error">
              {errors.description}
            </p>
          )}
        </div>

        <div className="form-actions">
          <Link to="/tickets" className="button ghost">
            Cancel
          </Link>
          <button type="submit" className="button primary" disabled={submitting}>
            {submitting ? 'Submitting…' : 'Submit ticket'}
          </button>
        </div>
      </form>
    </AppShell>
  )
}
