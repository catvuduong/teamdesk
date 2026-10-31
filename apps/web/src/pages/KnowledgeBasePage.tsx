import { useState } from 'react'
import { HelpShell } from '../components/HelpShell'
import { Link } from '../components/Link'
import { EmptyState, ErrorState, Loading } from '../components/States'
import { useDebounced, useLoadable } from '../hooks'
import { api } from '../lib/api'
import { CATEGORIES } from '../lib/constants'

/** Searches wait for a short pause in typing, so each keystroke does not send a request. */
const SEARCH_DELAY_MS = 300

export function KnowledgeBasePage() {
  const [query, setQuery] = useState('')
  const [category, setCategory] = useState('')
  const debouncedQuery = useDebounced(query, SEARCH_DELAY_MS)

  const articles = useLoadable(() => api.listArticles(debouncedQuery, category), [debouncedQuery, category])
  const searching = debouncedQuery.trim() !== ''

  return (
    <HelpShell>
      <header className="page-head">
        <div>
          <h1>Help centre</h1>
          <p className="muted">Answers to common questions. Most issues are solved here in a few minutes.</p>
        </div>
      </header>

      <form className="card stack" role="search" onSubmit={(event) => event.preventDefault()}>
        <label className="field">
          <span>Search articles</span>
          <input
            type="search"
            data-testid="kb-search"
            value={query}
            placeholder="For example: reset password"
            onChange={(event) => setQuery(event.target.value)}
          />
        </label>
        <div className="chip-row" role="group" aria-label="Category">
          <button type="button" className={category === '' ? 'chip active' : 'chip'} aria-pressed={category === ''} onClick={() => setCategory('')}>
            All
          </button>
          {CATEGORIES.map((name) => (
            <button
              key={name}
              type="button"
              className={category === name ? 'chip active' : 'chip'}
              aria-pressed={category === name}
              onClick={() => setCategory(name)}
            >
              {name}
            </button>
          ))}
        </div>
      </form>

      {articles.error && <ErrorState message={articles.error} onRetry={articles.reload} />}
      {articles.loading && !articles.data && <Loading label="Loading articles" />}

      {articles.data && articles.data.length === 0 && (
        <EmptyState title={searching ? 'No articles match your search' : 'No articles in this category yet'}>
          <p className="muted">Try other words, or open a ticket and we will help you directly.</p>
          <Link to="/tickets/new" className="button secondary">
            Open a ticket
          </Link>
        </EmptyState>
      )}

      {articles.data && articles.data.length > 0 && (
        <ul className="article-list" data-testid="kb-results" aria-label="Articles">
          {articles.data.map((article) => (
            <li key={article.id} className="card article-card">
              <span className="tag">{article.category}</span>
              <h2 className="card-title">
                <Link to={`/kb/${article.slug}`}>{article.title}</Link>
              </h2>
              <p className="muted">{article.excerpt}</p>
            </li>
          ))}
        </ul>
      )}
    </HelpShell>
  )
}
