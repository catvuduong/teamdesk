import { useEffect, useState } from 'react'
import { HelpShell } from '../components/HelpShell'
import { Link } from '../components/Link'
import { EmptyState, ErrorState, Loading } from '../components/States'
import { ApiError, api } from '../lib/api'
import { messageOf } from '../lib/errors'
import { Markdown } from '../lib/markdown'
import type { ArticleDetail } from '../lib/types'

interface ArticlePageProps {
  slug: string
}

export function ArticlePage({ slug }: ArticlePageProps) {
  const [article, setArticle] = useState<ArticleDetail | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [notFound, setNotFound] = useState(false)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let cancelled = false
    setArticle(null)
    setLoadError(null)
    setNotFound(false)
    api
      .getArticle(slug)
      .then((detail) => {
        if (!cancelled) setArticle(detail)
      })
      .catch((err) => {
        if (cancelled) return
        if (err instanceof ApiError && err.status === 404) setNotFound(true)
        else setLoadError(messageOf(err))
      })
    return () => {
      cancelled = true
    }
  }, [slug, attempt])

  if (notFound) {
    return (
      <HelpShell>
        <EmptyState title="Article not found">
          <p className="muted">It may have moved, or it is not published yet.</p>
          <Link to="/kb" className="button secondary">
            Back to the help centre
          </Link>
        </EmptyState>
      </HelpShell>
    )
  }

  if (loadError) {
    return (
      <HelpShell>
        <ErrorState message={loadError} onRetry={() => setAttempt((n) => n + 1)} />
      </HelpShell>
    )
  }

  if (!article) {
    return (
      <HelpShell>
        <Loading label="Loading article" />
      </HelpShell>
    )
  }

  return (
    <HelpShell>
      <nav className="breadcrumb" aria-label="Breadcrumb">
        <Link to="/kb">Help centre</Link> <span aria-hidden="true">/</span> <span>{article.category}</span>
      </nav>
      <article className="card article" aria-labelledby="article-title">
        <header>
          <span className="tag">{article.category}</span>
          <h1 id="article-title">{article.title}</h1>
          <p className="muted">{article.views} views</p>
        </header>
        <Markdown source={article.body} />
      </article>
    </HelpShell>
  )
}
