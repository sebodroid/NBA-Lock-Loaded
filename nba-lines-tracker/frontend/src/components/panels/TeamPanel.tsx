import { X } from 'lucide-react'
import { Card, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Separator } from '@/components/ui/separator'
import { useAppStore } from '@/store/useAppStore'
import { useTeamStats, useTeamGames } from '@/api/teams'
import { PanelStats } from './PanelStats'
import { GameLog } from './GameLog'

interface TeamPanelProps {
  teamId: number
}

export function TeamPanel({ teamId }: TeamPanelProps) {
  const closePanel = useAppStore(s => s.closePanel)
  const { data: teamDetail, isLoading: statsLoading, isError: statsError } = useTeamStats(teamId)
  const { data: games = [], isLoading: gamesLoading } = useTeamGames(teamId)

  const isLoading = statsLoading || gamesLoading

  return (
    <Card
      id={`panel-${teamId}`}
      className="w-96 flex-none h-[600px] overflow-y-auto scroll-mt-4"
    >
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2 sticky top-0 bg-card z-10 border-b">
        <CardTitle className="text-base font-semibold">
          {teamDetail ? (
            <>
              {teamDetail.name}
              <span className="ml-2 text-xs font-normal text-muted-foreground">
                {teamDetail.abbreviation}
              </span>
            </>
          ) : (
            <Skeleton className="h-5 w-32" />
          )}
        </CardTitle>
        <Button
          variant="ghost"
          size="icon"
          className="h-7 w-7 text-muted-foreground hover:text-foreground"
          onClick={() => closePanel(teamId)}
          aria-label="Close panel"
        >
          <X className="h-4 w-4" />
        </Button>
      </CardHeader>

      {statsError ? (
        <div className="p-4 text-sm text-destructive">
          Failed to load team data.
        </div>
      ) : isLoading ? (
        <div className="p-4 space-y-3">
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-3/4" />
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-2/3" />
        </div>
      ) : teamDetail ? (
        <>
          <PanelStats teamDetail={teamDetail} games={games} />
          <Separator />
          <GameLog games={games} teamAbbr={teamDetail.abbreviation} />
        </>
      ) : null}
    </Card>
  )
}
