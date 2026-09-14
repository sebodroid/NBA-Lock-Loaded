import { useEffect } from 'react'
import { Dialog } from 'radix-ui'
import { X } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Separator } from '@/components/ui/separator'
import { useAppStore } from '@/store/useAppStore'
import { usePlayerCard } from '@/api/players'
import type { PropEstimate } from '@/types/api'

function americanOdds(v: number | null): string {
  if (v === null) return '–'
  return v > 0 ? `+${v}` : `${v}`
}

function PropCard({ prop }: { prop: PropEstimate }) {
  const hasHistory = prop.gamesWithData > 0
  const hasOpponentContext = prop.opponentAllowedAverage !== null && prop.leagueAllowedAverage !== null

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold flex items-center justify-between">
          <span>{prop.marketLabel}</span>
          <span className="text-muted-foreground font-normal">
            O/U {prop.line} · vs {prop.opponentAbbreviation}
          </span>
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3 text-sm">
        <div className="flex justify-between text-xs text-muted-foreground">
          <span>Over {americanOdds(prop.overOdds)}</span>
          <span>Under {americanOdds(prop.underOdds)}</span>
          <span className="capitalize">{prop.bookmaker}</span>
        </div>

        <Separator />

        {!hasHistory && (
          <p className="text-xs text-muted-foreground italic">
            No games recorded yet this season for this stat.
          </p>
        )}

        {hasHistory && (
          <>
            <div>
              <p className="text-xs text-muted-foreground">
                {prop.statBasis === 'prior' ? '2025 season' : 'This season'}
              </p>
              <p>
                Averaging <span className="font-medium">{prop.seasonAverage?.toFixed(1)}</span> over{' '}
                {prop.gamesWithData} game{prop.gamesWithData === 1 ? '' : 's'} — went over the line in{' '}
                <span className="font-medium">
                  {prop.hitCount} of {prop.gamesWithData}
                </span>{' '}
                ({prop.hitRatePct}%)
              </p>
            </div>

            {hasOpponentContext ? (
              <div>
                <p className="text-xs text-muted-foreground">Opponent context</p>
                <p>
                  {prop.opponentAbbreviation} allows{' '}
                  <span className="font-medium">{prop.opponentAllowedAverage?.toFixed(1)}</span> per game
                  vs. a league average of {prop.leagueAllowedAverage?.toFixed(1)} —{' '}
                  {prop.opponentAllowedAverage! > prop.leagueAllowedAverage!
                    ? 'an easier-than-average matchup'
                    : 'a tougher-than-average matchup'}
                  .
                </p>
              </div>
            ) : (
              <p className="text-xs text-muted-foreground italic">
                Not enough league data yet to gauge this opponent's defense.
              </p>
            )}

            {prop.estimatedHitRatePct !== null ? (
              <div className="rounded-md bg-muted p-2">
                <p className="text-xs text-muted-foreground">
                  Estimated (opponent-adjusted){prop.statBasis === 'prior' ? ' · based on 2025' : ''}
                </p>
                <p className="font-medium">{prop.estimatedHitRatePct}% over this line</p>
              </div>
            ) : (
              <p className="text-xs text-muted-foreground italic">
                Not enough data yet for an opponent-adjusted estimate.
              </p>
            )}
          </>
        )}
      </CardContent>
    </Card>
  )
}

export function PlayerCardModal() {
  const selectedPlayerId = useAppStore(s => s.selectedPlayerId)
  const closePlayerCard = useAppStore(s => s.closePlayerCard)
  const { data: player, isLoading, isError } = usePlayerCard(selectedPlayerId)

  const open = selectedPlayerId !== null

  // Defensive cleanup for a known Radix + React StrictMode issue: the scroll-lock
  // Radix applies to <body> while a Dialog is open can occasionally fail to clear
  // on close (StrictMode's double-invoked effects lose track of the lock state),
  // leaving the entire page unclickable with no console error. Force-clear it
  // shortly after every close, catching the case whether or not Radix's own
  // cleanup ran correctly.
  useEffect(() => {
    if (open) return
    const timer = setTimeout(() => {
      document.body.style.pointerEvents = ''
      document.body.style.removeProperty('overflow')
    }, 300)
    return () => clearTimeout(timer)
  }, [open])

  return (
    <Dialog.Root open={open} onOpenChange={o => { if (!o) closePlayerCard() }}>
      <Dialog.Portal>
        {/* Overlay is the "click out to dismiss" surface — Radix wires this up automatically */}
        <Dialog.Overlay className="fixed inset-0 z-50 bg-black/50" />
        <Dialog.Content
          className="fixed left-1/2 top-1/2 z-50 w-[calc(100%-2rem)] max-w-lg max-h-[85vh] -translate-x-1/2
                     -translate-y-1/2 overflow-y-auto rounded-lg border bg-background p-6 shadow-lg
                     focus:outline-none"
        >
          <div className="flex items-start justify-between mb-4">
            <div>
              <Dialog.Title className="text-xl font-bold">
                {player?.name ?? (isLoading ? 'Loading…' : 'Player')}
              </Dialog.Title>
              {player && (
                <p className="text-sm text-muted-foreground">
                  {player.teamAbbreviation ?? 'Free agent'} · {player.gamesPlayed} game
                  {player.gamesPlayed === 1 ? '' : 's'} this season
                </p>
              )}
            </div>
            <Dialog.Close asChild>
              <Button variant="ghost" size="icon" className="h-7 w-7 shrink-0" aria-label="Close">
                <X className="h-4 w-4" />
              </Button>
            </Dialog.Close>
          </div>

          {isLoading && (
            <div className="space-y-3">
              <Skeleton className="h-20 w-full" />
              <Skeleton className="h-20 w-full" />
            </div>
          )}

          {isError && <p className="text-sm text-muted-foreground italic">Could not load this player.</p>}

          {player && (
            <div className="space-y-6">
              <div>
                <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-2">
                  Season Stats
                </p>
                {Object.keys(player.seasonStats).length === 0 ? (
                  <p className="text-sm text-muted-foreground italic">No stats recorded yet.</p>
                ) : (
                  <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
                    {Object.entries(player.seasonStats).map(([name, value]) => (
                      <div key={name} className="rounded-md border p-2">
                        <p className="text-xs text-muted-foreground">{name}</p>
                        <p className="text-lg font-semibold tabular-nums">{value}</p>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              <div>
                <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-2">
                  Active Betting Lines
                </p>
                {player.upcomingProps.length === 0 ? (
                  <p className="text-sm text-muted-foreground italic">
                    No active prop lines right now — sportsbooks typically post these closer to
                    kickoff, and coverage is thin during the preseason.
                  </p>
                ) : (
                  <div className="space-y-3">
                    {player.upcomingProps.map(p => (
                      <PropCard key={p.propLineId} prop={p} />
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
