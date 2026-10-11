import { useEffect, useMemo, useState } from 'react'
import { Dialog } from 'radix-ui'
import { format } from 'date-fns'
import { X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useAppStore } from '@/store/useAppStore'
import { useTeamProps } from '@/api/games'
import type { TeamPropMarket, TeamPropOutcome } from '@/types/api'

const GROUP_ORDER = ['Full Game', '1st Half', '1st Quarter']

const BOOK_NAMES: Record<string, string> = {
  fanduel: 'FanDuel',
  draftkings: 'DraftKings',
  betmgm: 'BetMGM',
  hardrockbet: 'Hard Rock Bet',
  williamhill_us: 'Caesars',
}

function americanOdds(v: number): string {
  return v > 0 ? `+${v}` : `${v}`
}

function signed(v: number): string {
  return v > 0 ? `+${v}` : `${v}`
}

interface OutcomeRow {
  key: string
  label: string
  outcomes: TeamPropOutcome[]
}

function toRows(market: TeamPropMarket): OutcomeRow[] {
  const rows = new Map<string, OutcomeRow>()
  for (const o of market.outcomes) {
    const key = o.teamId === null ? 'game' : String(o.teamId)
    const row = rows.get(key) ?? { key, label: o.teamId === null ? 'Game' : (o.teamAbbr ?? '–'), outcomes: [] }
    row.outcomes.push(o)
    rows.set(key, row)
  }
  return Array.from(rows.values())
}

function chipText(o: TeamPropOutcome): string {
  const odds = americanOdds(o.price)
  if (o.side === 'Over') return `O ${o.line} ${odds}`
  if (o.side === 'Under') return `U ${o.line} ${odds}`
  return o.line !== null ? `${signed(o.line)} ${odds}` : odds
}

export function TeamPropsModal() {
  const game = useAppStore(s => s.teamPropsGame)
  const closeTeamProps = useAppStore(s => s.closeTeamProps)
  const open = game !== null

  const { data: markets = [], isLoading, isError } = useTeamProps(game?.id ?? null)
  const [group, setGroup] = useState<string | null>(null)

  useEffect(() => { setGroup(null) }, [game?.id])

  useEffect(() => {
    if (open) return
    const t = setTimeout(() => {
      document.body.style.pointerEvents = ''
      document.body.style.removeProperty('overflow')
    }, 300)
    return () => clearTimeout(t)
  }, [open])

  const groups = useMemo(() => {
    const present = new Set(markets.map(m => m.group))
    return GROUP_ORDER.filter(g => present.has(g))
  }, [markets])

  const activeGroup = group && groups.includes(group) ? group : groups[0] ?? null
  const visible = markets.filter(m => m.group === activeGroup)

  const latest = markets.reduce<string | null>(
    (acc, m) => (acc === null || m.updatedAt > acc ? m.updatedAt : acc), null)

  return (
    <Dialog.Root open={open} onOpenChange={o => { if (!o) closeTeamProps() }}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 bg-black/50" />
        <Dialog.Content
          className="fixed left-1/2 top-1/2 z-50 w-[calc(100%-2rem)] max-w-lg max-h-[85vh] -translate-x-1/2
                     -translate-y-1/2 overflow-y-auto rounded-lg border bg-background p-6 shadow-lg
                     focus:outline-none"
        >
          <div className="flex items-start justify-between mb-4">
            <div>
              <Dialog.Title className="text-lg font-bold">Team Props</Dialog.Title>
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
            <p className="text-sm text-muted-foreground italic">Could not load team props for this game.</p>
          )}

          {!isLoading && !isError && markets.length === 0 && (
            <p className="text-sm text-muted-foreground italic">
              No team props posted for this game yet — sportsbooks usually put these up closer to kickoff.
            </p>
          )}

          {groups.length > 0 && activeGroup && (
            <>
              <div className="flex flex-wrap items-center gap-1 mb-4">
                {groups.map(g => (
                  <Button
                    key={g}
                    variant={g === activeGroup ? 'default' : 'outline'}
                    size="sm"
                    className="h-7 px-3 text-xs"
                    onClick={() => setGroup(g)}
                  >
                    {g}
                  </Button>
                ))}
              </div>

              <div className="space-y-3">
                {visible.map(m => (
                  <div key={m.marketKey} className="rounded-md border p-3">
                    <div className="flex items-center justify-between mb-1.5">
                      <span className="text-sm font-semibold">{m.label}</span>
                      <span className="text-xs text-muted-foreground">{BOOK_NAMES[m.bookmaker] ?? m.bookmaker}</span>
                    </div>
                    <table className="w-full text-xs">
                      <tbody>
                        {toRows(m).map(row => (
                          <tr key={row.key} className="border-t border-border/50">
                            <td className="py-1.5 pr-2 w-12 font-medium">{row.label}</td>
                            <td className="py-1.5">
                              <div className="flex flex-wrap gap-1">
                                {row.outcomes.map((o, i) => (
                                  <span key={i} className="rounded border px-1.5 py-0.5 tabular-nums whitespace-nowrap">
                                    {chipText(o)}
                                  </span>
                                ))}
                              </div>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ))}
              </div>

              <p className="mt-4 text-[11px] text-muted-foreground leading-relaxed">
                Books often disagree a lot on these markets, and each one only offers some of them — the
                sportsbook is named on every card.
                {latest && ` Lines as of ${format(new Date(latest), 'MMM d, h:mm a')}.`}
              </p>
            </>
          )}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
