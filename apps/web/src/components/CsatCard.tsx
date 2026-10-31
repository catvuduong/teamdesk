import { ErrorState, Loading } from './States'
import { useLoadable } from '../hooks'
import { api } from '../lib/api'
import { formatDayLabel } from '../lib/format'
import type { CsatStats, CsatWeek } from '../lib/types'

const WIDTH = 640
const HEIGHT = 200
const PAD = { top: 16, right: 12, bottom: 30, left: 36 }
const SCALE_MIN = 1
const SCALE_MAX = 5

/** Customer satisfaction: the average, how the scores spread, and the weekly trend. Loads its own data. */
export function CsatCard() {
  const csat = useLoadable(() => api.csatStats(), [])

  return (
    <section className="card csat" aria-labelledby="csat-title" data-testid="csat-card">
      <h2 id="csat-title" className="card-title">
        Customer satisfaction
      </h2>
      {csat.error && <ErrorState message={csat.error} onRetry={csat.reload} />}
      {csat.loading && !csat.data && <Loading label="Loading satisfaction" />}
      {csat.data && <CsatBody stats={csat.data} />}
    </section>
  )
}

function CsatBody({ stats }: { stats: CsatStats }) {
  if (stats.count === 0) return <p className="muted">No ratings yet.</p>

  const peak = Math.max(1, ...stats.distribution.map((entry) => entry.count))

  return (
    <>
      <p className="csat-headline">
        <strong data-testid="csat-average">{stats.average?.toFixed(1)}</strong>
        <span className="muted"> / 5 average from {stats.count} ratings</span>
      </p>

      <ul className="csat-distribution" aria-label="Scores">
        {[...stats.distribution].reverse().map((entry) => (
          <li key={entry.score}>
            <span className="csat-score">{entry.score} ★</span>
            <span className="bar-track" aria-hidden="true">
              <span className="bar-fill" style={{ width: `${(entry.count / peak) * 100}%` }} />
            </span>
            <span className="bar-value">{entry.count}</span>
          </li>
        ))}
      </ul>

      <TrendChart weeks={stats.trend} />
    </>
  )
}

/** Average score per week on a fixed 1 to 5 scale. Weeks with no ratings break the line instead of dropping to zero. */
function TrendChart({ weeks }: { weeks: CsatWeek[] }) {
  const plotWidth = WIDTH - PAD.left - PAD.right
  const plotHeight = HEIGHT - PAD.top - PAD.bottom
  const step = weeks.length > 1 ? plotWidth / (weeks.length - 1) : 0
  const xFor = (index: number) => PAD.left + index * step
  const yFor = (value: number) => PAD.top + plotHeight - ((value - SCALE_MIN) / (SCALE_MAX - SCALE_MIN)) * plotHeight

  const rated = weeks.filter((week) => week.average !== null)
  const summary =
    rated.length === 0
      ? `No ratings in the last ${weeks.length} weeks.`
      : `Weekly average over the last ${weeks.length} weeks, from ${rated[0].average?.toFixed(1)} to ${rated[rated.length - 1].average?.toFixed(1)}.`

  // One path per run of rated weeks, so a week with no ratings shows as a gap
  const runs: string[] = []
  let current: string[] = []
  weeks.forEach((week, index) => {
    if (week.average === null) {
      if (current.length) runs.push(current.join(' '))
      current = []
      return
    }
    current.push(`${current.length === 0 ? 'M' : 'L'}${xFor(index)},${yFor(week.average)}`)
  })
  if (current.length) runs.push(current.join(' '))

  return (
    <figure className="chart csat-trend">
      <svg className="chart-svg" viewBox={`0 0 ${WIDTH} ${HEIGHT}`} role="img" aria-labelledby="csat-trend-title csat-trend-desc">
        <title id="csat-trend-title">Average satisfaction per week</title>
        <desc id="csat-trend-desc">{summary}</desc>

        {[SCALE_MIN, 3, SCALE_MAX].map((tick) => (
          <g key={tick}>
            <line className="grid-line" x1={PAD.left} x2={WIDTH - PAD.right} y1={yFor(tick)} y2={yFor(tick)} />
            <text className="axis-text" x={PAD.left - 8} y={yFor(tick) + 4} textAnchor="end">
              {tick}
            </text>
          </g>
        ))}

        {runs.map((path) => (
          <path key={path} className="csat-line" d={path} fill="none" />
        ))}

        {weeks.map((week, index) =>
          week.average === null ? null : (
            <circle key={week.weekStart} className="csat-point" cx={xFor(index)} cy={yFor(week.average)} r={4}>
              <title>{`Week of ${formatDayLabel(week.weekStart)}: ${week.average.toFixed(1)} from ${week.count} ratings`}</title>
            </circle>
          ),
        )}

        {weeks.map((week, index) =>
          index % 2 === 1 || weeks.length <= 4 ? (
            <text key={`label-${week.weekStart}`} className="axis-text" x={xFor(index)} y={HEIGHT - 8} textAnchor="middle">
              {formatDayLabel(week.weekStart)}
            </text>
          ) : null,
        )}
      </svg>
      <figcaption className="sr-only">{summary}</figcaption>
    </figure>
  )
}
