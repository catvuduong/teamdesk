import type { PriorityBreach } from '../lib/types'
import { PRIORITY_LABEL } from '../lib/constants'

const WIDTH = 640
const HEIGHT = 240
const PAD = { top: 16, right: 8, bottom: 30, left: 40 }
const TICKS = [0, 25, 50, 75, 100]

/**
 * Breach rate per priority, as percent of that priority's tickets. One series, so one hue and no legend;
 * every bar carries its value as a direct label. Hand-drawn SVG, no chart library.
 */
export function SlaBreachChart({ rows }: { rows: PriorityBreach[] }) {
  const plotWidth = WIDTH - PAD.left - PAD.right
  const plotHeight = HEIGHT - PAD.top - PAD.bottom
  const groupWidth = plotWidth / Math.max(1, rows.length)
  const barWidth = Math.min(48, groupWidth * 0.5)
  const baseline = PAD.top + plotHeight
  const yFor = (percent: number) => baseline - (percent / 100) * plotHeight

  const summary = rows
    .map((row) => `${PRIORITY_LABEL[row.priority]}: ${formatRate(row.breachRate)} breached (${row.breached} of ${row.total})`)
    .join('. ')

  return (
    <figure className="chart sla-chart">
      <svg className="chart-svg" viewBox={`0 0 ${WIDTH} ${HEIGHT}`} role="img" aria-labelledby="sla-chart-title sla-chart-desc">
        <title id="sla-chart-title">Breach rate by priority</title>
        <desc id="sla-chart-desc">{summary}</desc>

        {TICKS.map((tick) => (
          <g key={tick}>
            <line className="grid-line" x1={PAD.left} x2={WIDTH - PAD.right} y1={yFor(tick)} y2={yFor(tick)} />
            <text className="axis-text" x={PAD.left - 8} y={yFor(tick) + 4} textAnchor="end">
              {tick}%
            </text>
          </g>
        ))}

        {rows.map((row, index) => {
          const center = PAD.left + index * groupWidth + groupWidth / 2
          const rate = row.breachRate ?? 0
          const height = (rate / 100) * plotHeight
          return (
            <g key={row.priority} className="bar-group">
              <title>{`${PRIORITY_LABEL[row.priority]}: ${row.breached} of ${row.total} breached`}</title>
              {row.breachRate !== null && (
                <rect
                  className="bar bar-breach"
                  x={center - barWidth / 2}
                  y={baseline - height}
                  width={barWidth}
                  height={height}
                  rx={4}
                />
              )}
              <text className="bar-label" x={center} y={row.breachRate === null ? baseline - 6 : baseline - height - 6} textAnchor="middle">
                {formatRate(row.breachRate)}
              </text>
              <text className="axis-text" x={center} y={HEIGHT - 8} textAnchor="middle">
                {PRIORITY_LABEL[row.priority]}
              </text>
            </g>
          )
        })}
      </svg>

      <table className="sr-only">
        <caption>Breach rate by priority</caption>
        <thead>
          <tr>
            <th scope="col">Priority</th>
            <th scope="col">Breached</th>
            <th scope="col">Tickets</th>
            <th scope="col">Breach rate</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.priority}>
              <th scope="row">{PRIORITY_LABEL[row.priority]}</th>
              <td>{row.breached}</td>
              <td>{row.total}</td>
              <td>{formatRate(row.breachRate)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  )
}

function formatRate(rate: number | null): string {
  return rate === null ? 'No tickets' : `${rate.toFixed(1)}%`
}
