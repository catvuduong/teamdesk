import { useEffect, useState, type FormEvent } from 'react'
import { useAuth } from '../auth/AuthContext'
import { AppShell } from '../components/AppShell'
import { PriorityBadge, StatusBadge } from '../components/Badges'
import { Link } from '../components/Link'
import { EmptyState, ErrorState, Loading } from '../components/States'
import { ApiError, api } from '../lib/api'
import { EVENT_LABEL, PRIORITIES, PRIORITY_LABEL, ROLE_LABEL, STATUS_LABEL } from '../lib/constants'
import { messageOf } from '../lib/errors'
import { formatDateTime, timeAgo } from '../lib/format'
import type { PublicUser, Status, Priority, TicketDetail, TicketPatch } from '../lib/types'

interface TicketDetailPageProps {
  id: number
}

export function TicketDetailPage({ id }: TicketDetailPageProps) {
  const { user } = useAuth()
  const isStaff = user?.role !== 'customer'

  const [ticket, setTicket] = useState<TicketDetail | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [notFound, setNotFound] = useState(false)
  const [attempt, setAttempt] = useState(0)

  const [actionError, setActionError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [people, setPeople] = useState<PublicUser[]>([])

  const [reply, setReply] = useState('')
  const [internal, setInternal] = useState(false)
  const [replyError, setReplyError] = useState<string | null>(null)
  const [sending, setSending] = useState(false)

  useEffect(() => {
    let cancelled = false
    setLoadError(null)
    setNotFound(false)
    api
      .getTicket(id)
      .then((detail) => {
        if (!cancelled) setTicket(detail)
      })
      .catch((err) => {
        if (cancelled) return
        if (err instanceof ApiError && err.status === 404) setNotFound(true)
        else setLoadError(messageOf(err))
      })
    return () => {
      cancelled = true
    }
  }, [id, attempt])

  useEffect(() => {
    if (!isStaff) return
    api
      .listUsers()
      .then((all) => setPeople(all.filter((u) => u.role !== 'customer')))
      .catch(() => setPeople([]))
  }, [isStaff])

  async function change(patch: TicketPatch) {
    setSaving(true)
    setActionError(null)
    try {
      setTicket(await api.updateTicket(id, patch))
    } catch (err) {
      setActionError(messageOf(err))
    } finally {
      setSaving(false)
    }
  }

  async function sendReply(event: FormEvent) {
    event.preventDefault()
    const body = reply.trim()
    if (!body) {
      setReplyError('Write a reply before sending')
      return
    }
    setSending(true)
    setReplyError(null)
    try {
      await api.addComment(id, body, isStaff && internal)
      setReply('')
      setInternal(false)
      setTicket(await api.getTicket(id))
    } catch (err) {
      setReplyError(messageOf(err))
    } finally {
      setSending(false)
    }
  }

  if (notFound) {
    return (
      <AppShell>
        <EmptyState title="Ticket not found">
          <p className="muted">It may have been removed, or it belongs to another customer.</p>
          <Link to="/tickets" className="button secondary">
            Back to tickets
          </Link>
        </EmptyState>
      </AppShell>
    )
  }

  if (loadError) {
    return (
      <AppShell>
        <ErrorState message={loadError} onRetry={() => setAttempt((n) => n + 1)} />
      </AppShell>
    )
  }

  if (!ticket) {
    return (
      <AppShell>
        <Loading label="Loading ticket" />
      </AppShell>
    )
  }

  const statusOptions = ticket.allowedStatuses

  return (
    <AppShell>
      <nav className="breadcrumb" aria-label="Breadcrumb">
        <Link to="/tickets">Tickets</Link> <span aria-hidden="true">/</span> <span>#{ticket.number}</span>
      </nav>

      <header className="ticket-head">
        <div className="ticket-head-main">
          <h1>{ticket.title}</h1>
          <div className="badge-row">
            <StatusBadge status={ticket.status} testId="ticket-status" />
            <PriorityBadge priority={ticket.priority} />
            <span className="tag">{ticket.category}</span>
          </div>
        </div>
      </header>

      <div className="ticket-layout">
        <div className="ticket-main-col">
          <section className="card" aria-labelledby="description-title">
            <h2 id="description-title" className="card-title">
              Description
            </h2>
            <p className="prose">{ticket.description}</p>
          </section>

          <section className="card" aria-labelledby="conversation-title">
            <h2 id="conversation-title" className="card-title">
              Conversation
            </h2>
            <ol className="thread" aria-label="Conversation">
              {ticket.comments.length === 0 && <li className="muted">No replies yet.</li>}
              {ticket.comments.map((comment) => (
                <li key={comment.id} className={comment.internal ? 'message internal' : 'message'}>
                  <header className="message-head">
                    <strong>{comment.author.name}</strong>
                    <span className="muted">
                      {ROLE_LABEL[comment.authorRole]} · {timeAgo(comment.createdAt)}
                    </span>
                    {comment.internal && <span className="tag tag-internal">Internal note</span>}
                  </header>
                  <p>{comment.body}</p>
                </li>
              ))}
            </ol>

            <form className="composer" onSubmit={sendReply} noValidate>
              <label className="field">
                <span className="visually-hidden">Reply</span>
                <textarea
                  data-testid="comment-input"
                  rows={3}
                  value={reply}
                  placeholder={isStaff && internal ? 'Internal note, visible to agents only' : 'Write a reply'}
                  onChange={(e) => setReply(e.target.value)}
                />
              </label>
              <div className="composer-actions">
                {isStaff && (
                  <label className="check">
                    <input type="checkbox" checked={internal} onChange={(e) => setInternal(e.target.checked)} />
                    Internal note
                  </label>
                )}
                <button type="submit" className="button primary" disabled={sending}>
                  {sending ? 'Sending…' : internal && isStaff ? 'Add note' : 'Send reply'}
                </button>
              </div>
              {replyError && (
                <p role="alert" className="form-error">
                  {replyError}
                </p>
              )}
            </form>
          </section>

          <section className="card" aria-labelledby="timeline-title">
            <h2 id="timeline-title" className="card-title">
              Timeline
            </h2>
            <ol className="timeline" aria-label="Ticket activity">
              {ticket.events.map((event) => (
                <li key={event.id} className="timeline-item" data-testid="timeline-item">
                  <span className="timeline-dot" aria-hidden="true" />
                  <div>
                    <p className="timeline-type">{EVENT_LABEL[event.type] ?? event.type}</p>
                    <p>{event.detail}</p>
                    <p className="muted">
                      {event.actor.name} · {formatDateTime(event.createdAt)}
                    </p>
                  </div>
                </li>
              ))}
            </ol>
          </section>
        </div>

        <aside className="ticket-side" aria-label="Ticket details">
          {isStaff && (
            <section className="card controls" aria-labelledby="controls-title">
              <h2 id="controls-title" className="card-title">
                Manage
              </h2>
              <label className="field">
                <span>Status</span>
                <select
                  data-testid="status-select"
                  value={ticket.status}
                  disabled={saving}
                  onChange={(e) => change({ status: e.target.value as Status })}
                >
                  <option value={ticket.status}>{STATUS_LABEL[ticket.status]} (current)</option>
                  {statusOptions.map((s) => (
                    <option key={s} value={s}>
                      {STATUS_LABEL[s]}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field">
                <span>Priority</span>
                <select
                  value={ticket.priority}
                  disabled={saving}
                  onChange={(e) => change({ priority: e.target.value as Priority })}
                >
                  {PRIORITIES.map((p) => (
                    <option key={p} value={p}>
                      {PRIORITY_LABEL[p]}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field">
                <span>Assignee</span>
                <select
                  value={ticket.assignee ? String(ticket.assignee.id) : ''}
                  disabled={saving}
                  onChange={(e) => change({ assigneeId: e.target.value ? Number(e.target.value) : null })}
                >
                  <option value="">Unassigned</option>
                  {people.map((p) => (
                    <option key={p.id} value={String(p.id)}>
                      {p.name}
                    </option>
                  ))}
                </select>
              </label>
              {saving && <p className="muted">Saving…</p>}
              {actionError && (
                <p role="alert" className="form-error">
                  {actionError}
                </p>
              )}
            </section>
          )}

          <section className="card" aria-labelledby="details-title">
            <h2 id="details-title" className="card-title">
              Details
            </h2>
            <dl className="details">
              <div>
                <dt>Requester</dt>
                <dd>{ticket.requester.name}</dd>
              </div>
              <div>
                <dt>Assignee</dt>
                <dd>{ticket.assignee ? ticket.assignee.name : 'Unassigned'}</dd>
              </div>
              <div>
                <dt>Created</dt>
                <dd>{formatDateTime(ticket.createdAt)}</dd>
              </div>
              <div>
                <dt>Updated</dt>
                <dd>{timeAgo(ticket.updatedAt)}</dd>
              </div>
              {ticket.resolvedAt && (
                <div>
                  <dt>Resolved</dt>
                  <dd>{formatDateTime(ticket.resolvedAt)}</dd>
                </div>
              )}
            </dl>
          </section>
        </aside>
      </div>
    </AppShell>
  )
}
