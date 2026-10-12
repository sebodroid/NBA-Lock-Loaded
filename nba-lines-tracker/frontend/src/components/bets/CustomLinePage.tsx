import { useEffect, useState } from 'react'
import { Search } from 'lucide-react'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Separator } from '@/components/ui/separator'
import { usePlayerSearch, useCustomEstimate, type PlayerSearchResult } from '@/api/customLine'
import { PROP_MARKETS } from '@/lib/propMarkets'

export function CustomLinePage() {
  const [query, setQuery] = useState('')
  const [debouncedQuery, setDebouncedQuery] = useState('')
  const [player, setPlayer] = useState<PlayerSearchResult | null>(null)
  const [marketKey, setMarketKey] = useState('')
  const [lineInput, setLineInput] = useState('')

  // Only the SEARCH is live-as-you-type (debounced) — the actual confidence check is a
  // deliberate submit, not fired on every keystroke of the line value.
  const [submitted, setSubmitted] = useState<{ playerId: number; marketKey: string; line: number } | null>(null)

  useEffect(() => {
    const t = setTimeout(() => setDebouncedQuery(query), 300)
    return () => clearTimeout(t)
  }, [query])

  const { data: results = [], isFetching: searching } = usePlayerSearch(player ? '' : debouncedQuery)
  const { data: estimate, isFetching: checking, isError } = useCustomEstimate(
    submitted?.playerId ?? null, submitted?.marketKey ?? null, submitted?.line ?? null
  )

  function handleCheck(e: React.FormEvent) {
    e.preventDefault()
    const line = parseFloat(lineInput)
    if (!player || !marketKey || Number.isNaN(line)) return
    setSubmitted({ playerId: player.id, marketKey, line })
  }

  const hasHistory = (estimate?.gamesWithData ?? 0) > 0

  return (
    <div className="max-w-lg">
      <p className="text-sm text-muted-foreground mb-4">
        Type in any player, stat, and number — even one no sportsbook has posted — and see how
        often that exact bet would have hit in real games this season, using the same math
        behind Hot Bets and Player Card. No betting experience needed: pick a player, pick
        what you're betting on (like "Receiving Yards"), type the number you're considering
        (like "65.5"), and we'll show you the history behind it.
      </p>

      <Card>
        <CardContent className="p-4 space-y-4">
          <form onSubmit={handleCheck} className="space-y-4">
            <div className="space-y-1.5 relative">
              <label className="text-xs font-medium text-muted-foreground">Player</label>
              <div className="relative">
                <Search className="absolute left-2.5 top-2.5 h-3.5 w-3.5 text-muted-foreground" />
                <Input
                  className="pl-8"
                  placeholder="Search a player…"
                  value={player ? player.name : query}
                  onChange={e => {
                    setPlayer(null)
                    setSubmitted(null)
                    setQuery(e.target.value)
                  }}
                />
              </div>
              {!player && debouncedQuery.trim().length >= 2 && (
                <div className="border rounded-md mt-1 max-h-48 overflow-y-auto">
                  {searching && <p className="p-2 text-xs text-muted-foreground">Searching…</p>}
                  {!searching && results.length === 0 && (
                    <p className="p-2 text-xs text-muted-foreground italic">No players found.</p>
                  )}
                  {results.map(r => (
                    <button
                      key={r.id}
                      type="button"
                      onClick={() => { setPlayer(r); setQuery('') }}
                      className="w-full text-left px-2 py-1.5 text-sm hover:bg-accent"
                    >
                      {r.name} {r.teamAbbreviation && <span className="text-muted-foreground">· {r.teamAbbreviation}</span>}
                    </button>
                  ))}
                </div>
              )}
            </div>

            <div className="space-y-1.5">
              <label className="text-xs font-medium text-muted-foreground">Market</label>
              <select
                value={marketKey}
                onChange={e => setMarketKey(e.target.value)}
                className="w-full h-9 rounded-md border bg-background px-3 text-sm"
              >
                <option value="">Select a market…</option>
                {['Receiving', 'Passing', 'Rushing', 'Touchdowns'].map(cat => (
                  <optgroup key={cat} label={cat}>
                    {PROP_MARKETS.filter(m => m.category === cat).map(m => (
                      <option key={m.key} value={m.key}>{m.label}</option>
                    ))}
                  </optgroup>
                ))}
              </select>
            </div>

            <div className="space-y-1.5">
              <label className="text-xs font-medium text-muted-foreground">Line</label>
              <Input
                type="number"
                step="0.5"
                placeholder="e.g. 1.5"
                value={lineInput}
                onChange={e => setLineInput(e.target.value)}
              />
            </div>

            <Button type="submit" className="w-full" disabled={!player || !marketKey || !lineInput}>
              Check confidence
            </Button>
          </form>

          {checking && <p className="text-xs text-muted-foreground italic">Checking…</p>}
          {isError && <p className="text-xs text-muted-foreground italic">Could not check this line.</p>}

          {estimate && !checking && (
            <>
              <Separator />
              {!hasHistory ? (
                <p className="text-xs text-muted-foreground italic">
                  No games recorded yet this season (or last season) for this stat — too early
                  to say anything useful about this one.
                </p>
              ) : (
                <div className="space-y-3 text-sm">
                  {/* Headline number — the one thing someone skimming actually needs */}
                  <div className="rounded-md bg-muted/50 p-3 text-center">
                    <p className="text-2xl font-bold text-primary">
                      {estimate.estimatedHitRatePct ?? estimate.hitRatePct}%
                    </p>
                    <p className="text-[11px] text-muted-foreground">
                      estimated chance this goes over the line
                      {estimate.estimatedHitRatePct !== null && ' (adjusted for the matchup below)'}
                    </p>
                  </div>

                  <div>
                    <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-1">
                      Season history
                    </p>
                    <p>
                      Averaging <span className="font-medium">{estimate.seasonAverage?.toFixed(1)}</span> over{' '}
                      {estimate.gamesWithData} game{estimate.gamesWithData === 1 ? '' : 's'}
                      {estimate.statBasis === 'prior' && (
                        <span className="text-[10px] text-muted-foreground"> (2025 season)</span>
                      )}{' '}
                      — went over this exact line in{' '}
                      <span className="font-medium">{estimate.hitCount} of {estimate.gamesWithData}</span>{' '}
                      of those games ({estimate.hitRatePct}%).
                    </p>
                  </div>

                  {estimate.recentGamesWithData > 0 && (
                    <div>
                      <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-1">
                        Recent form
                      </p>
                      <p>
                        Over his last {estimate.recentGamesWithData} games: went over in{' '}
                        <span className="font-medium">{estimate.recentHitRatePct}%</span> of them.{' '}
                        <span className="text-muted-foreground text-xs">
                          Worth checking against the season number above — a hot or cold stretch
                          can matter more than a full-season average, especially early in the year.
                        </span>
                      </p>
                    </div>
                  )}

                  {estimate.opportunityAvg !== null && (
                    <div>
                      <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-1">
                        How often he gets the ball
                      </p>
                      <p>
                        Averaging <span className="font-medium">{estimate.opportunityAvg} {estimate.opportunityLabel}</span>.{' '}
                        <span className="text-muted-foreground text-xs">
                          A high hit rate built on steady volume (lots of targets or touches every
                          week) is a safer bet than one built on a single big game — the player
                          actually has to be getting the opportunities for the trend to keep holding up.
                        </span>
                      </p>
                    </div>
                  )}

                  {estimate.opponentAllowedAverage !== null && estimate.leagueAllowedAverage !== null && (
                    <div>
                      <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-1">
                        This matchup
                      </p>
                      <p>
                        {estimate.opponentAbbreviation} allows{' '}
                        <span className="font-medium">{estimate.opponentAllowedAverage.toFixed(1)}</span> per game in
                        this stat, compared to a league average of {estimate.leagueAllowedAverage.toFixed(1)} —{' '}
                        {estimate.opponentAllowedAverage > estimate.leagueAllowedAverage
                          ? 'an easier-than-average matchup for this bet'
                          : 'a tougher-than-average matchup for this bet'}
                        . <span className="text-muted-foreground text-xs">
                          That's why the headline number above differs from the raw season rate —
                          it's adjusted for how generous or stingy this specific opponent tends to be.
                        </span>
                      </p>
                    </div>
                  )}
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
