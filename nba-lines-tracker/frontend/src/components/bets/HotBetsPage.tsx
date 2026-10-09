import { Flame } from 'lucide-react'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { InjuryBadge } from '@/components/ui/injury-badge'
import { useHotBets } from '@/api/hotBets'
import { useAppStore } from '@/store/useAppStore'

function americanOdds(v: number | null): string {
  if (v === null) return '–'
  return v > 0 ? `+${v}` : `${v}`
}

export function HotBetsPage() {
  const { data: hotBets = [], isLoading, isError } = useHotBets(10)
  const openPlayerCard = useAppStore(s => s.openPlayerCard)
  const openBetDraft = useAppStore(s => s.openBetDraft)

  return (
    <div>
      <p className="text-sm text-muted-foreground mb-4">
        The week's player props with the strongest estimated hit rate — always the{' '}
        <span className="font-medium">Over</span>, since that's what the estimate measures.
        Ranked highest-confidence first; only props with at least two games of history qualify.
      </p>

      {isLoading && (
        <div className="grid gap-3 sm:grid-cols-2">
          {[0, 1, 2, 3].map(i => <Skeleton key={i} className="h-28 w-full" />)}
        </div>
      )}

      {isError && <p className="text-sm text-muted-foreground italic">Could not load hot bets.</p>}

      {!isLoading && !isError && hotBets.length === 0 && (
        <p className="text-sm text-muted-foreground italic">
          Nothing clears the bar this week yet — check back once more games are in the book.
        </p>
      )}

      {!isLoading && !isError && hotBets.length > 0 && (
        <div className="grid gap-3 sm:grid-cols-2">
          {hotBets.map(hb => {
            const e = hb.estimate
            const pct = e.estimatedHitRatePct ?? e.hitRatePct
            return (
              <Card key={e.propLineId}>
                <CardContent className="p-4 space-y-2">
                  <div className="flex items-start justify-between">
                    <div>
                      <div className="flex items-center gap-1.5">
                        <button
                          onClick={() => openPlayerCard(hb.playerId)}
                          className="text-sm font-semibold hover:underline"
                        >
                          {hb.playerName}
                        </button>
                        <InjuryBadge status={hb.injuryStatus} note={hb.injuryNote} />
                      </div>
                      <p className="text-xs text-muted-foreground">
                        {hb.teamAbbreviation ?? '–'} · {hb.gameLabel} · {hb.gameDate}
                      </p>
                    </div>
                    <span className="flex items-center gap-1 rounded-full bg-gold/20 text-gold-foreground
                                      dark:text-gold text-xs font-semibold px-2 py-0.5">
                      <Flame className="h-3 w-3" /> {pct}%
                    </span>
                  </div>

                  <p className="text-sm">
                    {e.marketLabel} <span className="font-medium">Over {e.line}</span>
                    {e.statBasis === 'prior' && (
                      <span className="ml-1.5 text-[10px] text-muted-foreground" title="Based on the 2025 season">
                        2025
                      </span>
                    )}
                  </p>

                  <p className="text-xs text-muted-foreground">
                    Hit in {e.hitCount} of {e.gamesWithData} games vs {e.opponentAbbreviation}
                    {e.estimatedHitRatePct !== null ? ' (opponent-adjusted)' : ''}
                  </p>

                  <button
                    onClick={() => openBetDraft({
                      gameId: hb.gameId,
                      kind: 'PlayerProp',
                      playerPropLineId: e.propLineId,
                      side: 'Over',
                      label: `${hb.playerName} — ${e.marketLabel} Over ${e.line}`,
                      oddsPreview: e.overOdds ?? -110,
                    })}
                    title={`Best price via ${e.bookmaker}`}
                    className="w-full rounded border px-2 py-1.5 text-xs font-medium hover:bg-accent"
                  >
                    Bet Over {e.line} ({americanOdds(e.overOdds)}) <span className="capitalize text-muted-foreground">· {e.bookmaker}</span>
                  </button>
                </CardContent>
              </Card>
            )
          })}
        </div>
      )}
    </div>
  )
}
