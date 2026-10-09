import { useEffect, useMemo, useState } from 'react'
import { Dialog } from 'radix-ui'
import { X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { InjuryBadge } from '@/components/ui/injury-badge'
import { useAppStore } from '@/store/useAppStore'
import { useGameProps } from '@/api/games'
import type { GamePropEntry, InjuryStatus } from '@/types/api'

// Fixed tab order; anything the API tags "Other" collects unmapped markets.
const CATEGORY_ORDER = ['Receiving', 'Passing', 'Rushing', 'Touchdowns', 'Defense', 'Other']

function americanOdds(v: number | null): string {
  if (v === null) return '–'
  return v > 0 ? `+${v}` : `${v}`
}

interface PlayerGroup {
  playerId: number
  name: string
  team: string | null
  injuryStatus: InjuryStatus
  injuryNote: string | null
  lines: GamePropEntry[]
}

export function GamePropsModal() {
  const game = useAppStore(s => s.gamePropsGame)
  const closeGameProps = useAppStore(s => s.closeGameProps)
  const openPlayerCard = useAppStore(s => s.openPlayerCard)
  const openBetDraft = useAppStore(s => s.openBetDraft)
  const open = game !== null

  const { data: props = [], isLoading, isError } = useGameProps(game?.id ?? null)
  const [category, setCategory] = useState<string | null>(null)

  // Reset the selected tab whenever a different game's props open.
  useEffect(() => { setCategory(null) }, [game?.id])

  // Same defensive body-style cleanup as PlayerCardModal (Radix + StrictMode scroll-lock).
  useEffect(() => {
    if (open) return
    const t = setTimeout(() => {
      document.body.style.pointerEvents = ''
      document.body.style.removeProperty('overflow')
    }, 300)
    return () => clearTimeout(t)
  }, [open])

  // Coalesce a missing/unknown category to "Other" so an older API response (no
  // category field) still renders under a single tab instead of a blank panel.
  const categories = useMemo(() => {
    const present = new Set(props.map(p => p.estimate.category || 'Other'))
    const ordered = CATEGORY_ORDER.filter(c => present.has(c))
    return ordered.length > 0 ? ordered : (props.length > 0 ? ['Other'] : [])
  }, [props])

  const activeCategory = category && categories.includes(category) ? category : categories[0] ?? null

  const byPlayer = useMemo<PlayerGroup[]>(() => {
    const map = new Map<number, PlayerGroup>()
    for (const p of props) {
      if ((p.estimate.category || 'Other') !== activeCategory) continue
      const existing = map.get(p.playerId)
      if (existing) {
        existing.lines.push(p)
      } else {
        map.set(p.playerId, {
          playerId: p.playerId,
          name: p.playerName,
          team: p.teamAbbreviation,
          injuryStatus: p.injuryStatus,
          injuryNote: p.injuryNote,
          lines: [p],
        })
      }
    }
    return Array.from(map.values()).sort((a, b) => a.name.localeCompare(b.name))
  }, [props, activeCategory])

  const anyPrior = props.some(p => p.estimate.statBasis === 'prior')

  return (
    <Dialog.Root open={open} onOpenChange={o => { if (!o) closeGameProps() }}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 bg-black/50" />
        <Dialog.Content
          className="fixed left-1/2 top-1/2 z-50 w-[calc(100%-2rem)] max-w-lg max-h-[85vh] -translate-x-1/2
                     -translate-y-1/2 overflow-y-auto rounded-lg border bg-background p-6 shadow-lg
                     focus:outline-none"
        >
          <div className="flex items-start justify-between mb-4">
            <div>
              <Dialog.Title className="text-lg font-bold">Player Props</Dialog.Title>
              {game && <p className="text-sm text-muted-foreground">{game.title}</p>}
            </div>
            <Dialog.Close asChild>
              <Button variant="ghost" size="icon" className="h-7 w-7 shrink-0" aria-label="Close">
                <X className="h-4 w-4" />
              </Button>
            </Dialog.Close>
          </div>

          {isLoading && (
            <div className="space-y-3">
              <Skeleton className="h-8 w-full" />
              <Skeleton className="h-24 w-full" />
            </div>
          )}

          {isError && (
            <p className="text-sm text-muted-foreground italic">Could not load props for this game.</p>
          )}

          {!isLoading && !isError && props.length === 0 && (
            <p className="text-sm text-muted-foreground italic">
              No player props posted for this game yet — sportsbooks usually put these up closer to kickoff.
            </p>
          )}

          {categories.length > 0 && activeCategory && (
            <>
              <div className="flex flex-wrap items-center gap-1 mb-4">
                {categories.map(c => (
                  <Button
                    key={c}
                    variant={c === activeCategory ? 'default' : 'outline'}
                    size="sm"
                    className="h-7 px-3 text-xs"
                    onClick={() => setCategory(c)}
                  >
                    {c}
                  </Button>
                ))}
              </div>

              <div className="space-y-3">
                {byPlayer.map(p => (
                  <div key={p.playerId} className="rounded-md border p-3">
                    <div className="flex items-center justify-between mb-1.5">
                      <div className="flex items-center gap-1.5">
                        <button
                          onClick={() => openPlayerCard(p.playerId)}
                          className="text-sm font-semibold hover:underline"
                        >
                          {p.name}
                        </button>
                        <InjuryBadge status={p.injuryStatus} note={p.injuryNote} />
                      </div>
                      <span className="text-xs text-muted-foreground">{p.team ?? '–'}</span>
                    </div>
                    <table className="w-full text-xs">
                      <tbody>
                        {p.lines.map(l => {
                          const e = l.estimate
                          const pct = e.estimatedHitRatePct ?? e.hitRatePct
                          return (
                            <tr key={e.propLineId} className="border-t border-border/50">
                              <td className="py-1.5 pr-2">{e.marketLabel}</td>
                              <td className="py-1.5 pr-2 tabular-nums text-muted-foreground whitespace-nowrap">
                                O/U {e.line}
                              </td>
                              <td className="py-1.5 pr-2 whitespace-nowrap">
                                <div className="flex gap-1">
                                  <button
                                    disabled={e.overOdds === null}
                                    title={`Best price via ${e.bookmaker}`}
                                    onClick={() => openBetDraft({
                                      gameId: game!.id,
                                      kind: 'PlayerProp',
                                      playerPropLineId: e.propLineId,
                                      side: 'Over',
                                      label: `${p.name} — ${e.marketLabel} Over ${e.line}`,
                                      oddsPreview: e.overOdds ?? -110,
                                    })}
                                    className="rounded border px-1.5 py-0.5 tabular-nums hover:bg-accent
                                               disabled:opacity-40 disabled:pointer-events-none"
                                  >
                                    O {americanOdds(e.overOdds)}
                                  </button>
                                  <button
                                    disabled={e.underOdds === null}
                                    title={e.underBookmaker ? `Best price via ${e.underBookmaker}` : undefined}
                                    onClick={() => openBetDraft({
                                      gameId: game!.id,
                                      kind: 'PlayerProp',
                                      playerPropLineId: e.propLineId,
                                      side: 'Under',
                                      label: `${p.name} — ${e.marketLabel} Under ${e.line}`,
                                      oddsPreview: e.underOdds ?? -110,
                                    })}
                                    className="rounded border px-1.5 py-0.5 tabular-nums hover:bg-accent
                                               disabled:opacity-40 disabled:pointer-events-none"
                                  >
                                    U {americanOdds(e.underOdds)}
                                  </button>
                                </div>
                              </td>
                              <td
                                className="py-1.5 text-right tabular-nums whitespace-nowrap"
                                title={e.recentGamesWithData > 0 ? `Last ${e.recentGamesWithData}: ${e.recentHitRatePct}% over` : undefined}
                              >
                                {pct !== null ? (
                                  <>
                                    {pct}%
                                    {e.statBasis === 'prior' && (
                                      <span className="ml-1 text-[10px] text-muted-foreground" title="Based on the 2025 season">
                                        2025
                                      </span>
                                    )}
                                  </>
                                ) : '–'}
                              </td>
                            </tr>
                          )
                        })}
                      </tbody>
                    </table>
                  </div>
                ))}
              </div>

              <p className="mt-4 text-[11px] text-muted-foreground leading-relaxed">
                <span className="font-medium">Est.</span> is the opponent-adjusted rate the player went over
                this line, or the raw rate when there isn't enough matchup data.
                {anyPrior && ' A "2025" tag means the estimate is based on last season, until enough games are played this year.'}
              </p>
            </>
          )}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
