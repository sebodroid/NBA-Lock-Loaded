import { useEffect, useMemo, useState } from 'react'
import { Dialog } from 'radix-ui'
import { X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useRecentGames, useGamePlayers } from '@/api/games'
import { useCreateBet } from '@/api/bets'
import { PROP_MARKETS } from '@/lib/propMarkets'
import { cn } from '@/lib/utils'
import type { CreateBetRequest } from '@/types/api'

type Kind = 'PlayerProp' | 'Spread' | 'Total'

interface ManualBetModalProps {
  open: boolean
  onClose: () => void
}

// Logs a bet placed somewhere else (another book, in person) — the app didn't track the
// line, so you type in what you actually got. As long as it's tied to a real game/player
// this app knows about, grading and CLV work exactly like an auto-tracked bet once the
// game plays out; only the line/odds themselves came from you instead of a sync.
export function ManualBetModal({ open, onClose }: ManualBetModalProps) {
  const [gameId, setGameId] = useState<number | null>(null)
  const [kind, setKind] = useState<Kind>('PlayerProp')
  const [playerId, setPlayerId] = useState<number | null>(null)
  const [marketKey, setMarketKey] = useState('')
  const [side, setSide] = useState<'Over' | 'Under' | 'Home' | 'Away'>('Over')
  const [line, setLine] = useState('')
  const [odds, setOdds] = useState('-110')
  const [stake, setStake] = useState('25')

  const { data: games = [] } = useRecentGames(14)
  const { data: players = [] } = useGamePlayers(gameId)
  const createBet = useCreateBet()

  const selectedGame = games.find(g => g.gameId === gameId)

  // Reset the fields that depend on the game/kind whenever either changes, so a leftover
  // player/market from a previous selection can't get silently submitted.
  useEffect(() => { setPlayerId(null); setMarketKey('') }, [gameId])
  useEffect(() => {
    setSide(kind === 'Spread' ? 'Home' : 'Over')
    createBet.reset()
  }, [kind]) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (!open) {
      setGameId(null); setKind('PlayerProp'); setPlayerId(null); setMarketKey('')
      setSide('Over'); setLine(''); setOdds('-110'); setStake('25')
      createBet.reset()
    }
  }, [open]) // eslint-disable-line react-hooks/exhaustive-deps

  // Same defensive body-style cleanup as the other modals (Radix + StrictMode scroll-lock).
  useEffect(() => {
    if (open) return
    const t = setTimeout(() => {
      document.body.style.pointerEvents = ''
      document.body.style.removeProperty('overflow')
    }, 300)
    return () => clearTimeout(t)
  }, [open])

  const marketsByCategory = useMemo(() => {
    const map = new Map<string, typeof PROP_MARKETS>()
    for (const m of PROP_MARKETS) {
      const list = map.get(m.category) ?? []
      list.push(m)
      map.set(m.category, list)
    }
    return map
  }, [])

  const lineNum = parseFloat(line)
  const oddsNum = parseInt(odds, 10)
  const stakeNum = parseFloat(stake)
  const canSubmit = gameId !== null
    && !Number.isNaN(lineNum) && !Number.isNaN(oddsNum) && stakeNum > 0
    && (kind !== 'PlayerProp' || (playerId !== null && marketKey !== ''))

  async function handleSubmit() {
    if (!canSubmit || gameId === null) return

    const request: CreateBetRequest = {
      gameId,
      kind,
      side,
      stakeAmount: stakeNum,
      manualLine: lineNum,
      manualOdds: oddsNum,
      ...(kind === 'PlayerProp' ? { playerId, marketKey } : {}),
    }

    try {
      await createBet.mutateAsync(request)
      onClose()
    } catch {
      // surfaced inline below via createBet.isError
    }
  }

  return (
    <Dialog.Root open={open} onOpenChange={o => { if (!o) onClose() }}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-[60] bg-black/50" />
        <Dialog.Content
          className="fixed left-1/2 top-1/2 z-[60] w-[calc(100%-2rem)] max-w-md max-h-[85vh] overflow-y-auto
                     -translate-x-1/2 -translate-y-1/2 rounded-lg border bg-background p-6 shadow-lg
                     focus:outline-none"
        >
          <div className="flex items-start justify-between mb-4">
            <div>
              <Dialog.Title className="text-lg font-bold">Log a Bet</Dialog.Title>
              <p className="text-xs text-muted-foreground">Placed on another book? Add it here to track it.</p>
            </div>
            <Dialog.Close asChild>
              <Button variant="ghost" size="icon" className="h-7 w-7 shrink-0" aria-label="Close">
                <X className="h-4 w-4" />
              </Button>
            </Dialog.Close>
          </div>

          <div className="space-y-4">
            {/* Game */}
            <div>
              <label className="block text-xs font-medium text-muted-foreground mb-1.5">Game</label>
              <select
                value={gameId ?? ''}
                onChange={e => setGameId(e.target.value ? Number(e.target.value) : null)}
                className="w-full rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
              >
                <option value="">Select a game…</option>
                {games.map(g => (
                  <option key={g.gameId} value={g.gameId}>
                    {g.gameDate} · {g.awayTeamAbbr} @ {g.homeTeamAbbr} ({g.status})
                  </option>
                ))}
              </select>
            </div>

            {/* Kind */}
            <div>
              <label className="block text-xs font-medium text-muted-foreground mb-1.5">Bet type</label>
              <div className="flex gap-1.5">
                {(['PlayerProp', 'Spread', 'Total'] as Kind[]).map(k => (
                  <Button
                    key={k}
                    type="button"
                    variant={kind === k ? 'default' : 'outline'}
                    size="sm"
                    className="flex-1 text-xs"
                    onClick={() => setKind(k)}
                  >
                    {k === 'PlayerProp' ? 'Player Prop' : k}
                  </Button>
                ))}
              </div>
            </div>

            {kind === 'PlayerProp' && (
              <>
                <div>
                  <label className="block text-xs font-medium text-muted-foreground mb-1.5">Player</label>
                  <select
                    value={playerId ?? ''}
                    onChange={e => setPlayerId(e.target.value ? Number(e.target.value) : null)}
                    disabled={gameId === null}
                    className="w-full rounded-md border bg-background px-3 py-2 text-sm disabled:opacity-50 focus:outline-none focus:ring-2 focus:ring-ring"
                  >
                    <option value="">{gameId === null ? 'Pick a game first' : 'Select a player…'}</option>
                    {players.map(p => (
                      <option key={p.playerId} value={p.playerId}>
                        {p.name} {p.teamAbbreviation ? `(${p.teamAbbreviation})` : ''}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-medium text-muted-foreground mb-1.5">Market</label>
                  <select
                    value={marketKey}
                    onChange={e => setMarketKey(e.target.value)}
                    className="w-full rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                  >
                    <option value="">Select a market…</option>
                    {Array.from(marketsByCategory.entries()).map(([category, markets]) => (
                      <optgroup key={category} label={category}>
                        {markets.map(m => (
                          <option key={m.key} value={m.key}>{m.label}</option>
                        ))}
                      </optgroup>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-medium text-muted-foreground mb-1.5">Side</label>
                  <div className="flex gap-1.5">
                    {(['Over', 'Under'] as const).map(s => (
                      <Button
                        key={s}
                        type="button"
                        variant={side === s ? 'default' : 'outline'}
                        size="sm"
                        className="flex-1 text-xs"
                        onClick={() => setSide(s)}
                      >
                        {s}
                      </Button>
                    ))}
                  </div>
                </div>
              </>
            )}

            {kind === 'Spread' && (
              <div>
                <label className="block text-xs font-medium text-muted-foreground mb-1.5">Side</label>
                <div className="flex gap-1.5">
                  <Button
                    type="button"
                    variant={side === 'Home' ? 'default' : 'outline'}
                    size="sm"
                    className="flex-1 text-xs"
                    onClick={() => setSide('Home')}
                  >
                    {selectedGame?.homeTeamAbbr ?? 'Home'}
                  </Button>
                  <Button
                    type="button"
                    variant={side === 'Away' ? 'default' : 'outline'}
                    size="sm"
                    className="flex-1 text-xs"
                    onClick={() => setSide('Away')}
                  >
                    {selectedGame?.awayTeamAbbr ?? 'Away'}
                  </Button>
                </div>
              </div>
            )}

            {kind === 'Total' && (
              <div>
                <label className="block text-xs font-medium text-muted-foreground mb-1.5">Side</label>
                <div className="flex gap-1.5">
                  {(['Over', 'Under'] as const).map(s => (
                    <Button
                      key={s}
                      type="button"
                      variant={side === s ? 'default' : 'outline'}
                      size="sm"
                      className="flex-1 text-xs"
                      onClick={() => setSide(s)}
                    >
                      {s}
                    </Button>
                  ))}
                </div>
              </div>
            )}

            {/* Line / Odds / Stake */}
            <div className="grid grid-cols-3 gap-2">
              <div>
                <label className="block text-xs font-medium text-muted-foreground mb-1.5">
                  {kind === 'Spread' ? 'Line (signed)' : 'Line'}
                </label>
                <input
                  type="number" step="0.5" value={line} onChange={e => setLine(e.target.value)}
                  placeholder={kind === 'Spread' ? '-3.5' : '4.5'}
                  className={cn(
                    'w-full rounded-md border bg-background px-2.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring',
                  )}
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-muted-foreground mb-1.5">Odds</label>
                <input
                  type="number" step="5" value={odds} onChange={e => setOdds(e.target.value)}
                  className="w-full rounded-md border bg-background px-2.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-muted-foreground mb-1.5">Stake ($)</label>
                <input
                  type="number" step="1" min="1" value={stake} onChange={e => setStake(e.target.value)}
                  className="w-full rounded-md border bg-background px-2.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                />
              </div>
            </div>

            {kind === 'Spread' && (
              <p className="text-[11px] text-muted-foreground -mt-2">
                Enter the number as it appeared on your slip for the side you picked — negative if you were
                giving points, positive if you were getting them.
              </p>
            )}

            {createBet.isError && (
              <p className="text-xs text-red-600 dark:text-red-400">
                Could not save this bet — double check the game and player are right.
              </p>
            )}

            <Button className="w-full" disabled={!canSubmit || createBet.isPending} onClick={handleSubmit}>
              {createBet.isPending ? 'Saving…' : 'Add Bet'}
            </Button>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
