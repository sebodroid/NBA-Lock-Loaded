import { getPercentageColor } from '@/lib/color'

interface AtsCellProps {
  pct: number | null
}

export function AtsCell({ pct }: AtsCellProps) {
  if (pct === null) return <span className="text-muted-foreground tabular-nums">–</span>
  return (
    <span
      style={getPercentageColor(pct)}
      className="inline-block px-2 py-0.5 rounded text-sm font-medium tabular-nums"
    >
      {pct.toFixed(1)}%
    </span>
  )
}
