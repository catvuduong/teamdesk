import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { AppShell } from '../components/AppShell'
import { ErrorState, Loading, SkeletonTable } from '../components/States'
import { api } from '../lib/api'
import { ROLE_LABEL } from '../lib/constants'
import { messageOf } from '../lib/errors'
import { initials } from '../lib/format'
import type { PublicUser, Role } from '../lib/types'

const ROLES: Role[] = ['customer', 'agent', 'admin']

/** Everyone can see the team; only admins can change roles. */
export function UsersPage() {
  const { user: me } = useAuth()
  const isAdmin = me?.role === 'admin'

  const [users, setUsers] = useState<PublicUser[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)
  const [pending, setPending] = useState<number | null>(null)
  const [rowError, setRowError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setError(null)
    api
      .listUsers()
      .then((all) => {
        if (!cancelled) setUsers(all)
      })
      .catch((err) => {
        if (!cancelled) setError(messageOf(err))
      })
    return () => {
      cancelled = true
    }
  }, [attempt])

  async function changeRole(target: PublicUser, role: Role) {
    setPending(target.id)
    setRowError(null)
    try {
      const updated = await api.updateUserRole(target.id, role)
      setUsers((current) => current?.map((u) => (u.id === updated.id ? updated : u)) ?? null)
    } catch (err) {
      setRowError(messageOf(err))
    } finally {
      setPending(null)
    }
  }

  return (
    <AppShell>
      <header className="page-head">
        <div>
          <h1>Users</h1>
          <p className="muted">{isAdmin ? 'Change a role to give or remove access to the queue.' : 'The people in this workspace.'}</p>
        </div>
      </header>

      {error && <ErrorState message={error} onRetry={() => setAttempt((n) => n + 1)} />}
      {!error && !users && (
        <div className="card table-card">
          <Loading label="Loading users">
            <SkeletonTable rows={4} />
          </Loading>
        </div>
      )}
      {rowError && (
        <p role="alert" className="form-error">
          {rowError}
        </p>
      )}

      {users && (
        <div className="card table-card">
          <table className="data-table stack-table">
            <caption className="visually-hidden">Workspace members</caption>
            <thead>
              <tr>
                <th scope="col">Name</th>
                <th scope="col">Email</th>
                <th scope="col">Role</th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => (
                <tr key={u.id}>
                  <td data-label="Name">
                    <span className="person">
                      <span className="avatar small" aria-hidden="true">
                        {initials(u.name)}
                      </span>
                      {u.name}
                    </span>
                  </td>
                  <td data-label="Email" className="muted">
                    {u.email}
                  </td>
                  <td data-label="Role">
                    {isAdmin ? (
                      <label className="visually-hidden" htmlFor={`role-${u.id}`}>
                        Role for {u.name}
                      </label>
                    ) : null}
                    {isAdmin ? (
                      <select
                        id={`role-${u.id}`}
                        value={u.role}
                        disabled={pending === u.id || u.id === me?.id}
                        onChange={(e) => void changeRole(u, e.target.value as Role)}
                      >
                        {ROLES.map((r) => (
                          <option key={r} value={r}>
                            {ROLE_LABEL[r]}
                          </option>
                        ))}
                      </select>
                    ) : (
                      <span className="tag">{ROLE_LABEL[u.role]}</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </AppShell>
  )
}
