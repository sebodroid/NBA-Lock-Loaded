import { useMemo, useRef, useState } from 'react'
import { useAppStore } from '@/store/useAppStore'
import type { BetResponse } from '@/types/api'

interface Point {
  index: number
  bet: BetResponse
  delta: number       // this bet's contribution to bankroll (+toWin / -stake / 0)
  cumulative: number  // running total through this bet
}

const WIDTH = 640
const HEIGHT = 180
const PAD_LEFT = 44
const PAD_RIGHT = 16
const PAD_TOP = 16
const PAD_BOTTOM = 24

function outcomeColor(outcome: string, isDark: boolean): string {
  if (outcome === 'Won')  return isDark ? '#4ade80' : '#16a34a'   // green-400 / green-600
  if (outcome === 'Lost') return isDark ? '#f87171' : '#dc2626'   // red-400 / red-600
  return isDark ? '#a1a1aa' : '#71717a'                            // Push — zinc-400 / zinc-500
}

// Bet-by-bet bankroll equity curve. Only graded bets (Won/Lost/Push) move the line —
// Pending bets haven't affected the bankroll yet, so they're excluded rather than
// plotted at zero. Every value here is also in the bets table above/below this chart,
// so there's no separate "table view" to build — this chart doesn't gate any data.
export function BankrollChart({ bets }: { bets: BetResponse[] }) {
  const isDark = useAppStore(s => s.theme === 'dark')
  const svgRef = useRef<SVGSVGElement>(null)
  const [hoverIndex, setHoverIndex] = useState<number | null>(null)

  const points = useMemo<Point[]>(() => {
    const graded = bets
      .filter(b => b.outcome !== 'Pending')
      .slice()
      .sort((a, b) => new Date(a.placedAt).getTime() - new Date(b.placedAt).getTime())

    let running = 0
    return graded.map((bet, i) => {
      const delta = bet.outcome === 'Won' ? bet.toWinAmount : bet.outcome === 'Lost' ? -bet.stakeAmount : 0
      running += delta
      return { index: i, bet, delta, cumulative: running }
    })
  }, [bets])

  if (points.length === 0) {
    return (
      <p className="text-sm text-muted-foreground italic">
        Bankroll trend fills in once your first bet is graded.
      </p>
    )
  }

  const values = [0, ...points.map(p => p.cumulative)]
  const yMin = Math.min(...values)
  const yMax = Math.max(...values)
  const ySpan = yMax - yMin || 1
  const yPad = ySpan * 0.1

  const plotW = WIDTH - PAD_LEFT - PAD_RIGHT
  const plotH = HEIGHT - PAD_TOP - PAD_BOTTOM

  const xFor = (i: number) => PAD_LEFT + (points.length === 1 ? plotW / 2 : (i / (points.length - 1)) * plotW)
  const yFor = (v: number) => PAD_TOP + plotH - ((v - (yMin - yPad)) / (ySpan + yPad * 2)) * plotH

  const zeroY = yFor(0)
  const linePath = points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${xFor(i).toFixed(1)} ${yFor(p.cumulative).toFixed(1)}`).join(' ')

  const final = points[points.length - 1]
  const lineColor = 'var(--primary)'
  const gridColor = isDark ? 'oklch(1 0.01 300 / 20%)' : 'oklch(0.5 0.02 300 / 25%)'

  function handlePointerMove(e: React.PointerEvent<SVGSVGElement>) {
    const svg = svgRef.current
    if (!svg) return
    const rect = svg.getBoundingClientRect()
    const x = ((e.clientX - rect.left) / rect.width) * WIDTH
    let nearest = 0
    let nearestDist = Infinity
    points.forEach((_, i) => {
      const dist = Math.abs(xFor(i) - x)
      if (dist < nearestDist) { nearestDist = dist; nearest = i }
    })
    setHoverIndex(nearest)
  }

  const hovered = hoverIndex !== null ? points[hoverIndex] : null
  const tooltipLeft = hovered ? (xFor(hovered.index) / WIDTH) * 100 : 0
  const tooltipAlignRight = tooltipLeft > 65

  return (
    <div>
      <div className="flex items-baseline justify-between mb-1">
        <p className="text-xs font-medium text-muted-foreground">Bankroll (cumulative)</p>
        <p className={`text-sm font-semibold tabular-nums ${final.cumulative > 0 ? 'text-green-600 dark:text-green-400' : final.cumulative < 0 ? 'text-red-600 dark:text-red-400' : 'text-muted-foreground'}`}>
          {final.cumulative >= 0 ? '+' : ''}${final.cumulative.toFixed(2)}
        </p>
      </div>

      <div className="relative">
        <svg
          ref={svgRef}
          viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
          className="w-full h-auto touch-none"
          onPointerMove={handlePointerMove}
          onPointerLeave={() => setHoverIndex(null)}
        >
          {/* Zero baseline — recessive hairline, solid per spec */}
          <line x1={PAD_LEFT} y1={zeroY} x2={WIDTH - PAD_RIGHT} y2={zeroY} stroke={gridColor} strokeWidth={1} />
          <text x={PAD_LEFT - 6} y={zeroY} textAnchor="end" dominantBaseline="middle" className="fill-muted-foreground" fontSize={10}>
            $0
          </text>

          {/* Crosshair */}
          {hovered && (
            <line
              x1={xFor(hovered.index)} y1={PAD_TOP} x2={xFor(hovered.index)} y2={HEIGHT - PAD_BOTTOM}
              stroke={gridColor} strokeWidth={1}
            />
          )}

          {/* Equity line */}
          <path d={linePath} fill="none" stroke={lineColor} strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />

          {/* Markers — colored by that bet's outcome, 2px surface ring so they read
              clearly where the line crosses them */}
          {points.map(p => (
            <circle
              key={p.index}
              cx={xFor(p.index)}
              cy={yFor(p.cumulative)}
              r={hoverIndex === p.index ? 5 : 4}
              fill={outcomeColor(p.bet.outcome, isDark)}
              stroke="var(--card)"
              strokeWidth={2}
            />
          ))}

          {/* Invisible wide hit-strip so hover works across the whole chart, not just on marks */}
          <rect x={PAD_LEFT} y={0} width={plotW} height={HEIGHT} fill="transparent" />

          {/* Endpoint label */}
          <text
            x={xFor(final.index)}
            y={yFor(final.cumulative) - 10}
            textAnchor={final.index === points.length - 1 ? 'end' : 'middle'}
            className="fill-foreground font-medium"
            fontSize={11}
          >
            {final.cumulative >= 0 ? '+' : ''}${final.cumulative.toFixed(0)}
          </text>
        </svg>

        {hovered && (
          <div
            className="absolute top-0 pointer-events-none rounded-md border bg-popover text-popover-foreground shadow-md px-2.5 py-1.5 text-xs whitespace-nowrap"
            style={{
              left: `${tooltipLeft}%`,
              transform: tooltipAlignRight ? 'translateX(-100%)' : 'translateX(0)',
            }}
          >
            <p className="font-semibold tabular-nums">
              {hovered.cumulative >= 0 ? '+' : ''}${hovered.cumulative.toFixed(2)}
            </p>
            <p className="text-muted-foreground">
              {new Date(hovered.bet.placedAt).toLocaleDateString()} · {hovered.bet.outcome}{' '}
              ({hovered.delta >= 0 ? '+' : ''}${hovered.delta.toFixed(2)})
            </p>
          </div>
        )}
      </div>
    </div>
  )
}
