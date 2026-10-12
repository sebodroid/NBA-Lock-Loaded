import { useState } from 'react'
import { Layers } from 'lucide-react'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Separator } from '@/components/ui/separator'
import { useParlays } from '@/api/parlays'
import type { ParlayLeg } from '@/types/api'

function americanOdds(v: number): string {
  return v > 0 ? `+${v}` : `${v}`
}

export function ParlaysPage() {
  const [legSize, setLegSize] = useState<2 | 3>(2)
  const { data: parlays = [], isLoading, isError } = useParlays(5, legSize)

  return (
    <div>
      <p className="text-sm text-muted-foreground mb-4">
        Suggested combinations of this week's best props and team-total leans, ranked by{' '}
        <span className="font-medium">our own combined confidence</span> — not the sportsbook's
        implied odds. Each leg already passed Hot Bets' own hit-rate, recent-form, and
        volume checks on its own; combining legs only ever understates the real hit chance
        (correlated legs, like a team total and its own player's prop, aren't accounted for),
        never overstates it.
      </p>

      <div className="flex items-center gap-1 mb-4">
        {([2, 3] as const).map(n => (
          <Button
            key={n}
            variant={n === legSize ? 'default' : 'outline'}
            size="sm"
            className="h-7 px-3 text-xs"
            onClick={() => setLegSize(n)}
          >
            {n}-leg
          </Button>
        ))}
      </div>

      {isLoading && (
        <div className="grid gap-3 sm:grid-cols-2">
          {[0, 1, 2].map(i => <Skeleton key={i} className="h-36 w-full" />)}
        </div>
      )}

      {isError && <p className="text-sm text-muted-foreground italic">Could not load parlay suggestions.</p>}

      {!isLoading && !isError && parlays.length === 0 && (
        <p className="text-sm text-muted-foreground italic">
          Not enough strong legs this week to suggest a {legSize}-leg parlay — check back once more
          games are in the book, or try the {legSize === 2 ? '3' : '2'}-leg view.
        </p>
      )}

      {!isLoading && !isError && parlays.length > 0 && (
        <div className="grid gap-3 sm:grid-cols-2">
          {parlays.map((p, i) => (
            <Card key={i}>
              <CardContent className="p-4 space-y-2">
                <div className="flex items-start justify-between">
                  <span className="flex items-center gap-1 rounded-full bg-gold/20 text-gold-foreground
                                    dark:text-gold text-xs font-semibold px-2 py-0.5">
                    <Layers className="h-3 w-3" /> {p.combinedProbabilityPct}%
                  </span>
                  <span className="text-xs text-muted-foreground tabular-nums">
                    {americanOdds(p.combinedOdds)}
                  </span>
                </div>

                <Separator />

                <div className="space-y-1.5">
                  {p.legs.map((leg, j) => <LegRow key={j} leg={leg} />)}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}

function LegRow({ leg }: { leg: ParlayLeg }) {
  return (
    <div className="text-xs">
      <p>{leg.description}</p>
      <p className="text-muted-foreground">
        {leg.gameLabel} · {leg.confidencePct}% · {americanOdds(leg.odds)}
      </p>
    </div>
  )
}
