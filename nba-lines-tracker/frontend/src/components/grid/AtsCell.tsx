import { getPercentageColor } from '@/lib/color'
import { useAppStore } from '@/store/useAppStore'

interface AtsCellProps {
  pct: number | null
}

export function AtsCell({ pct }: AtsCellProps) {
  // Subscribed (not read off document.documentElement) so this re-renders — and
  // re-colors — the instant the theme toggle changes, not just on the next data refetch.
  const isDark = useAppStore(s => s.theme === 'dark')
  if (pct === null) return <span className="text-muted-foreground tabular-nums">–</span>
  return (
    <span
      style={getPercentageColor(pct, isDark)}
      className="inline-block px-2 py-0.5 rounded text-sm font-medium tabular-nums"
    >
      {pct.toFixed(1)}%
    </span>
  )
}
