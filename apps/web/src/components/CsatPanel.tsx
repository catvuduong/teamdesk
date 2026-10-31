import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import { formatDayLabel } from '../lib/format'
import { messageOf } from '../lib/errors'
import type { CsatStats, CsatWeek } from '../lib/types'
import { ErrorState, Loading } from './States'

const WIDTH = 640
const HEIGHT = 240
const PAD = { top: 16, right: 24, bottom: 30, left: 36 }
/** Ratings run from 1 to 5, so the y axis is fixed to that range rather than fitted to the data. */
const SCALE_MIN = 1
const SCALE_MAX = 5

/** Customer satisfaction on the dashboard: the average, how the scores spread, and the weekly trend. */
export function CsatPanel() {
  const [stats, setStats] = useState<CsatStats | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let cancelled = false
    setError(null)
    api
      .csatStats()
      .then((data) => {
        if (!cancelled) setStats(data)
      })
      .catch((err) => {
        if (!cancelled) setError(messageOf(err))
      })
    return () => {
      cancelled = true
    }
  }, [attempt])

  return (
    <section className="card csat" aria-labelledby="csat-title">
      <h2 id="csat-title" className="card-title">
        Customer satisfaction
      </h2>

      {error && <ErrorState message={error} onRetry={() => setAttempt((n) => n + 1)} />}
      {!error && !stats && <Loading label="Loading satisfaction" />}

      {stats && (
        <div className="csat-body">
          <div className="csat-summary">
            <p className="csat-average" data-testid="csat-average">
              {stats.average === null ? '—' : stats.average.toFixed(1)}
              <span className="csat-out-of"> / 5</span>
            </p>
            <p className="muted">
              {stats.count === 0 ? 'No ratings yet' : `From ${stats.count} ${stats.count === 1 ? 'rating' : 'ratings'}`}
            </p>
          </div>

          <div className="csat-distribution">
            <h3 className="csat-subtitle">Scores</h3>
            <ul className="bars">
              {[...stats.distribution].reverse().map((row) => {
                const largest = Math.max(1, ...stats.distribution.map((r) => r.count))
                return (
                  <li key={row.score} className="bar-row">
                    <span className="bar-label">{row.score} stars</span>
                    <span className="bar-track" aria-hidden="true">
                      <span className="bar-fill csat-fill" style={{ width: `${(row.count / largest) * 100}%` }} />
                    </span>
                    <span className="bar-value">
                      <span className="visually-hidden">{row.score} stars: </span>
                      {row.count}
                    </span>
                  </li>
                )
              })}
            </ul>
          </div>
        </div>
      )}

      {stats && <CsatTrend weeks={stats.trend} />}
    </section>
  )
}

/**
 * Average score per week as a line over the last weeks. One series, so one hue and no legend. The latest point is
 * labelled directly, and every week is listed in the table for screen readers. Weeks with no ratings break the line.
 */
function CsatTrend({ weeks }: { weeks: CsatWeek[] }) {
  const plotWidth = WIDTH - PAD.left - PAD.right
  const plotHeight = HEIGHT - PAD.top - PAD.bottom
  const yFor = (score: number) => PAD.top + plotHeight - ((score - SCALE_MIN) / (SCALE_MAX - SCALE_MIN)) * plotHeight
  const xFor = (index: number) => PAD.left + ((index + 0.5) / weeks.length) * plotWidth
  const ticks = Array.from({ length: SCALE_MAX - SCALE_MIN + 1 }, (_, i) => SCALE_MIN + i)

  let path = ''
  let startSegment = true
  weeks.forEach((week, index) => {
    if (week.average === null) {
      startSegment = true
      return
    }
    path += `${startSegment ? 'M' : 'L'}${xFor(index)} ${yFor(week.average)} `
    startSegment = false
  })

  const latestIndex = weeks.findLastIndex((week) => week.average !== null)
  const latest = latestIndex >= 0 ? weeks[latestIndex] : null
  const summary =
    latest?.average != null
      ? `Average score by week over the last ${weeks.length} weeks. Latest week of ${formatDayLabel(latest.weekStart)}: ${latest.average.toFixed(1)} from ${latest.count} ratings.`
      : `Average score by week over the last ${weeks.length} weeks. No ratings in this period.`

  return (
    <figure className="chart csat-chart">
      <figcaption className="csat-subtitle">Average score per week</figcaption>
      <svg className="chart-svg" viewBox={`0 0 ${WIDTH} ${HEIGHT}`} role="img" aria-labelledby="csat-trend-title csat-trend-desc">
        <title id="csat-trend-title">Average satisfaction per week</title>
        <desc id="csat-trend-desc">{summary}</desc>

        {ticks.map((tick) => (
          <g key={tick}>
            <line className="grid-line" x1={PAD.left} x2={WIDTH - PAD.right} y1={yFor(tick)} y2={yFor(tick)} />
            <text className="axis-text" x={PAD.left - 8} y={yFor(tick) + 4} textAnchor="end">
              {tick}
            </text>
          </g>
        ))}

        {path && <path className="trend-line" d={path} fill="none" />}

        {weeks.map((week, index) => {
          const x = xFor(index)
          return (
            <g key={week.weekStart}>
              {week.average !== null && (
                <circle className="trend-point" cx={x} cy={yFor(week.average)} r={4.5}>
                  <title>{`Week of ${formatDayLabel(week.weekStart)}: ${week.average.toFixed(1)} from ${week.count} ratings`}</title>
                </circle>
              )}
              <text className="axis-text" x={x} y={HEIGHT - 8} textAnchor="middle">
                {formatDayLabel(week.weekStart)}
              </text>
            </g>
          )
        })}

        {latest?.average != null && latestIndex >= 0 && (
          <text className="bar-label" x={xFor(latestIndex)} y={yFor(latest.average) - 10} textAnchor="middle">
            {latest.average.toFixed(1)}
          </text>
        )}
      </svg>

      <table className="sr-only">
        <caption>Average score per week</caption>
        <thead>
          <tr>
            <th scope="col">Week of</th>
            <th scope="col">Ratings</th>
            <th scope="col">Average</th>
          </tr>
        </thead>
        <tbody>
          {weeks.map((week) => (
            <tr key={week.weekStart}>
              <td>{formatDayLabel(week.weekStart)}</td>
              <td>{week.count}</td>
              <td>{week.average === null ? 'no ratings' : week.average.toFixed(1)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  )
}
