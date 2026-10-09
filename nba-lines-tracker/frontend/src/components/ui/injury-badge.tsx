import { Badge } from '@/components/ui/badge'
import type { InjuryStatus } from '@/types/api'

const STYLES: Record<string, string> = {
  Questionable: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400 border-0',
  Doubtful: 'bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-400 border-0',
  Out: 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400 border-0',
  'Injured Reserve': 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400 border-0',
}

const ABBR: Record<string, string> = {
  Questionable: 'Q',
  Doubtful: 'D',
  Out: 'OUT',
  'Injured Reserve': 'IR',
}

// Renders nothing when the player isn't on the injury report at all — a missing badge
// is the "healthy" state, not a loading state.
export function InjuryBadge({ status, note }: { status: InjuryStatus; note?: string | null }) {
  if (!status) return null
  return (
    <Badge
      variant="outline"
      title={note ?? status}
      className={`text-[10px] px-1 py-0 shrink-0 ${STYLES[status] ?? 'bg-muted text-muted-foreground border-0'}`}
    >
      {ABBR[status] ?? status}
    </Badge>
  )
}
