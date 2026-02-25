import { format } from 'date-fns'
import { ScrollArea, ScrollBar } from '@/components/ui/scroll-area'
import { Skeleton } from '@/components/ui/skeleton'
import { useTodayMatchups } from '@/api/games'
import { MatchupCard } from './MatchupCard'

export function MatchupsSection() {
  const { data: matchups, isLoading, isError } = useTodayMatchups()

  const todayLabel = format(new Date(), 'EEEE, MMMM d')  // e.g. "Wednesday, February 26"

  return (
    <div className="mb-6 border-b pb-6">
      <div className="flex items-center justify-between mb-3">
        <p className="text-sm font-medium text-muted-foreground">
          Today's Games
          <span className="ml-2 text-xs text-muted-foreground/60">{todayLabel}</span>
        </p>
        {matchups && matchups.length > 0 && (
          <span className="text-xs text-muted-foreground/60">
            {matchups.length} {matchups.length === 1 ? 'game' : 'games'}
          </span>
        )}
      </div>

      {isLoading && (
        <div className="flex gap-4">
          {[0, 1, 2].map(i => (
            <Skeleton key={i} className="w-80 h-64 flex-none rounded-lg" />
          ))}
        </div>
      )}

      {isError && (
        <p className="text-sm text-muted-foreground italic">Could not load today's games.</p>
      )}

      {!isLoading && !isError && matchups?.length === 0 && (
        <p className="text-sm text-muted-foreground italic">No games scheduled for today.</p>
      )}

      {!isLoading && !isError && matchups && matchups.length > 0 && (
        <ScrollArea className="w-full">
          <div className="flex gap-4 pb-4">
            {matchups.map(m => (
              <MatchupCard key={m.gameId} matchup={m} />
            ))}
          </div>
          <ScrollBar orientation="horizontal" />
        </ScrollArea>
      )}
    </div>
  )
}
