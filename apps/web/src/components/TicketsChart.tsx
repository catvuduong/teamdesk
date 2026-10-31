import type { DayCount } from '../types'
import { formatDay } from '../format'

const WIDTH = 640
const HEIGHT = 240
const PAD = { top: 16, right: 8, bottom: 30, left: 36 }

/** Largest tick value at or above `max`, in a 1-2-5 sequence, so gridlines land on round numbers. */
export function niceCeiling(max: number): number {
  if (max <= 4) return 4
  const magnitude = 10 ** Math.floor(Math.log10(max))
  for (const step of [1, 2, 2.5, 5, 10]) {
    if (step * magnitude >= max) return step * magnitude
  }
  return 10 * magnitude
}

/** Grouped bars per UTC day: created (accent) against resolved (green). Hand-drawn SVG, no chart library. */
export function TicketsChart({ days }: { days: DayCount[] }) {
  const plotWidth = WIDTH - PAD.left - PAD.right
  const plotHeight = HEIGHT - PAD.top - PAD.bottom
  const peak = Math.max(1, ...days.flatMap((day) => [day.created, day.resolved]))
  const top = niceCeiling(peak)
  const groupWidth = plotWidth / Math.max(1, days.length)
  const barWidth = Math.min(16, groupWidth * 0.32)
  const yFor = (value: number) => PAD.top + plotHeight - (value / top) * plotHeight

  const totalCreated = days.reduce((sum, day) => sum + day.created, 0)
  const totalResolved = days.reduce((sum, day) => sum + day.resolved, 0)
  const summary = `Last ${days.length} days: ${totalCreated} tickets created, ${totalResolved} resolved.`

  const ticks = [0, 1, 2, 3, 4].map((step) => (top / 4) * step)

  return (
    <figure className="chart">
      <div className="chart-legend" aria-hidden="true">
        <span className="legend-item">
          <span className="swatch swatch-created" /> Created
        </span>
        <span className="legend-item">
          <span className="swatch swatch-resolved" /> Resolved
        </span>
      </div>
      <svg
        className="chart-svg"
        viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
        role="img"
        aria-labelledby="chart-title chart-desc"
      >
        <title id="chart-title">Tickets per day, last {days.length} days</title>
        <desc id="chart-desc">{summary}</desc>

        {ticks.map((tick) => (
          <g key={tick}>
            <line className="grid-line" x1={PAD.left} x2={WIDTH - PAD.right} y1={yFor(tick)} y2={yFor(tick)} />
            <text className="axis-text" x={PAD.left - 8} y={yFor(tick) + 4} textAnchor="end">
              {Math.round(tick)}
            </text>
          </g>
        ))}

        {days.map((day, index) => {
          const groupX = PAD.left + index * groupWidth
          const center = groupX + groupWidth / 2
          const createdHeight = (day.created / top) * plotHeight
          const resolvedHeight = (day.resolved / top) * plotHeight
          const baseline = PAD.top + plotHeight
          return (
            <g key={day.date} className="bar-group">
              <title>{`${formatDay(day.date)}: ${day.created} created, ${day.resolved} resolved`}</title>
              <rect
                className="bar bar-created"
                x={center - barWidth - 1}
                y={baseline - createdHeight}
                width={barWidth}
                height={createdHeight}
                rx={3}
              />
              <rect
                className="bar bar-resolved"
                x={center + 1}
                y={baseline - resolvedHeight}
                width={barWidth}
                height={resolvedHeight}
                rx={3}
              />
              {index % 2 === 1 || days.length <= 7 ? (
                <text className="axis-text" x={center} y={HEIGHT - 8} textAnchor="middle">
                  {formatDay(day.date)}
                </text>
              ) : null}
            </g>
          )
        })}
      </svg>
      <figcaption className="sr-only">{summary}</figcaption>
    </figure>
  )
}
