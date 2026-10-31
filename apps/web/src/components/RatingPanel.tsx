import { useState, type FormEvent } from 'react'
import { api } from '../lib/api'
import { formatDateTime } from '../lib/format'
import { messageOf } from '../lib/errors'
import type { RatingView, TicketDetail } from '../lib/types'

const RATABLE: TicketDetail['status'][] = ['resolved', 'closed']
const SCORES = [5, 4, 3, 2, 1]

interface RatingPanelProps {
  ticket: TicketDetail
  isRequester: boolean
  onRated: (rating: RatingView) => void
}

/** Shows the rating once given. The requester is asked for one on a resolved or closed ticket. */
export function RatingPanel({ ticket, isRequester, onRated }: RatingPanelProps) {
  const [score, setScore] = useState<number | null>(null)
  const [comment, setComment] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  if (ticket.rating) {
    return (
      <section className="card rating" aria-labelledby="rating-title" data-testid="rating-given">
        <h2 id="rating-title" className="card-title">
          {isRequester ? 'Your rating' : 'Customer rating'}
        </h2>
        <p>
          <strong>{ticket.rating.score} / 5</strong> <span className="muted">· {formatDateTime(ticket.rating.createdAt)}</span>
        </p>
        {ticket.rating.comment && <p className="prose">{ticket.rating.comment}</p>}
      </section>
    )
  }

  if (!isRequester || !RATABLE.includes(ticket.status)) return null

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (score === null) {
      setError('Choose a score from 1 to 5')
      return
    }
    setSaving(true)
    setError(null)
    try {
      onRated(await api.rateTicket(ticket.id, score, comment.trim()))
    } catch (err) {
      setError(messageOf(err))
      setSaving(false)
    }
  }

  return (
    <section className="card rating" aria-labelledby="rating-title">
      <h2 id="rating-title" className="card-title">
        How did we do?
      </h2>
      <p className="muted">Rate how well this ticket was resolved. You can rate it once.</p>
      <form className="stack" onSubmit={submit} noValidate>
        <fieldset className="rating-scale">
          <legend className="visually-hidden">Score</legend>
          {SCORES.map((value) => (
            <label key={value} className="rating-option">
              <input
                type="radio"
                name="rating-score"
                value={value}
                checked={score === value}
                onChange={() => setScore(value)}
              />
              {value} {value === 1 ? 'star' : 'stars'}
            </label>
          ))}
        </fieldset>
        <label className="field">
          <span>Comment (optional)</span>
          <textarea rows={3} maxLength={1000} value={comment} onChange={(e) => setComment(e.target.value)} />
        </label>
        {error && (
          <p role="alert" className="form-error">
            {error}
          </p>
        )}
        <div className="form-actions">
          <button type="submit" className="button primary" disabled={saving}>
            {saving ? 'Sending…' : 'Send rating'}
          </button>
        </div>
      </form>
    </section>
  )
}
