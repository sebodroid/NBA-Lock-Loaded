import { Flame, TrendingUp } from 'lucide-react'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { InjuryBadge } from '@/components/ui/injury-badge'
import { useHotBets } from '@/api/hotBets'
import { useAppStore } from '@/store/useAppStore'
import type { HotBetEntry } from '@/types/api'

function americanOdds(v: number | null): string {
  if (v === null) return '–'
  return v > 0 ? `+${v}` : `${v}`
}

function HotBetCard({ hb, developing }: { hb: HotBetEntry; developing: boolean }) {
  const e = hb.estimate
  const isOver = hb.side === 'Over'
  const odds = isOver ? e.overOdds : e.underOdds
  const bookmaker = isOver ? e.bookmaker : (e.underBookmaker ?? e.bookmaker)
  const openPlayerCard = useAppStore(s => s.openPlayerCard)
  const openBetDraft = useAppStore(s => s.openBetDraft)

  return (
    <Card className={developing ? 'border-dashed opacity-90' : undefined}>
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
          <span className={`flex items-center gap-1 rounded-full text-xs font-semibold px-2 py-0.5 ${
            developing
              ? 'bg-muted text-muted-foreground'
              : 'bg-gold/20 text-gold-foreground dark:text-gold'
          }`}>
            {developing ? <TrendingUp className="h-3 w-3" /> : <Flame className="h-3 w-3" />} {hb.displayHitRatePct}%
          </span>
        </div>

        <p className="text-sm">
          {e.marketLabel} <span className="font-medium">{hb.side} {e.line}</span>
          {e.statBasis === 'prior' && (
            <span className="ml-1.5 text-[10px] text-muted-foreground" title="Based on the 2025 season">
              2025
            </span>
          )}
        </p>

        <p className="text-xs text-muted-foreground">
          {isOver ? 'Hit' : 'Stayed under'} in {hb.displayHitCount} of {e.gamesWithData} games vs {e.opponentAbbreviation}
          {e.opponentDefensiveRank !== null && e.teamsRanked !== null
            ? ` (ranked ${e.opponentDefensiveRank} of ${e.teamsRanked} vs. this stat)`
            : e.estimatedHitRatePct !== null ? ' (opponent-adjusted)' : ''}
        </p>

        {developing && (
          <p className="text-xs text-muted-foreground italic">
            Only {e.gamesWithData} games of history — the percentage could move a lot with the
            next game or two, so this isn't confident enough to count as a Hot Bet yet.
          </p>
        )}

        {e.recentGamesWithData > 0 && (
          <p className="text-xs text-muted-foreground">
            Last {e.recentGamesWithData}: {e.recentHitRatePct}% over
          </p>
        )}

        {e.opportunityAvg !== null && (
          <p className="text-xs text-muted-foreground">
            {e.opportunityAvg} {e.opportunityLabel}
          </p>
        )}

        <button
          onClick={() => openBetDraft({
            gameId: hb.gameId,
            kind: 'PlayerProp',
            playerPropLineId: e.propLineId,
            side: hb.side,
            label: `${hb.playerName} — ${e.marketLabel} ${hb.side} ${e.line}`,
            oddsPreview: odds ?? -110,
          })}
          title={`Best price via ${bookmaker}`}
          className="w-full rounded border px-2 py-1.5 text-xs font-medium hover:bg-accent"
        >
          Bet {hb.side} {e.line} ({americanOdds(odds)}) <span className="capitalize text-muted-foreground">· {bookmaker}</span>
        </button>
      </CardContent>
    </Card>
  )
}

export function HotBetsPage() {
  const { data, isLoading, isError } = useHotBets(10)
  const confirmed = data?.confirmed ?? []
  const developing = data?.developing ?? []

  return (
    <div>
      <p className="text-sm text-muted-foreground mb-4">
        The week's player props with the strongest estimated hit rate, Over or Under — whichever
        side the data actually supports. Ranked by a confidence bound that discounts small
        samples, not the raw percentage — a player who's cleared the line in their only 2 games
        won't outrank one who's cleared it 6 of 8 times, even though both can show a high number.
      </p>

      {isLoading && (
        <div className="grid gap-3 sm:grid-cols-2">
          {[0, 1, 2, 3].map(i => <Skeleton key={i} className="h-28 w-full" />)}
        </div>
      )}

      {isError && <p className="text-sm text-muted-foreground italic">Could not load hot bets.</p>}

      {!isLoading && !isError && confirmed.length === 0 && developing.length === 0 && (
        <p className="text-sm text-muted-foreground italic">
          Nothing clears the bar this week yet — check back once more games are in the book.
        </p>
      )}

      {!isLoading && !isError && confirmed.length > 0 && (
        <div className="grid gap-3 sm:grid-cols-2">
          {confirmed.map(hb => <HotBetCard key={`${hb.estimate.propLineId}-${hb.side}`} hb={hb} developing={false} />)}
        </div>
      )}

      {!isLoading && !isError && developing.length > 0 && (
        <div className="mt-6">
          <h3 className="text-sm font-semibold mb-1">Developing</h3>
          <p className="text-xs text-muted-foreground mb-3">
            Good early signs, but not enough games yet to clear the confidence bar above —
            worth watching, not yet worth trusting the way a Hot Bet is.
          </p>
          <div className="grid gap-3 sm:grid-cols-2">
            {developing.map(hb => <HotBetCard key={`${hb.estimate.propLineId}-${hb.side}`} hb={hb} developing />)}
          </div>
        </div>
      )}
    </div>
  )
}
