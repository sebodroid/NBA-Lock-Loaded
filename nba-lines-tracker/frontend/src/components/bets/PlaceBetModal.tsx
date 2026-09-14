import { useEffect, useState } from 'react'
import { Dialog } from 'radix-ui'
import { X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useAppStore } from '@/store/useAppStore'
import { useCreateBet } from '@/api/bets'

function americanOdds(v: number): string {
  return v > 0 ? `+${v}` : `${v}`
}

// Client-side mirror of the API's ComputeToWin — a preview only. The server always
// snapshots the actual live odds at submit time, so this can be a beat stale without
// the placed bet itself ever being wrong.
function toWinPreview(stake: number, odds: number): number {
  if (!stake || stake <= 0) return 0
  const raw = odds > 0 ? (stake * odds) / 100 : (stake * 100) / Math.abs(odds)
  return Math.round(raw * 100) / 100
}

export function PlaceBetModal() {
  const draft = useAppStore(s => s.betDraft)
  const closeBetDraft = useAppStore(s => s.closeBetDraft)
  const createBet = useCreateBet()
  const [stake, setStake] = useState('25')
  const open = draft !== null

  useEffect(() => {
    if (open) setStake('25')
    createBet.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draft?.label, draft?.side])

  // Same defensive body-style cleanup as the other modals (Radix + StrictMode scroll-lock).
  useEffect(() => {
    if (open) return
    const t = setTimeout(() => {
      document.body.style.pointerEvents = ''
      document.body.style.removeProperty('overflow')
    }, 300)
    return () => clearTimeout(t)
  }, [open])

  const stakeNum = parseFloat(stake) || 0
  const toWin = draft ? toWinPreview(stakeNum, draft.oddsPreview) : 0

  async function handleConfirm() {
    if (!draft || stakeNum <= 0) return
    try {
      await createBet.mutateAsync({
        gameId: draft.gameId,
        kind: draft.kind,
        playerPropLineId: draft.playerPropLineId ?? null,
        side: draft.side,
        stakeAmount: stakeNum,
      })
      closeBetDraft()
    } catch {
      // surfaced inline below via createBet.isError
    }
  }

  return (
    <Dialog.Root open={open} onOpenChange={o => { if (!o) closeBetDraft() }}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-[60] bg-black/50" />
        <Dialog.Content
          className="fixed left-1/2 top-1/2 z-[60] w-[calc(100%-2rem)] max-w-sm -translate-x-1/2
                     -translate-y-1/2 rounded-lg border bg-background p-6 shadow-lg focus:outline-none"
        >
          <div className="flex items-start justify-between mb-4">
            <Dialog.Title className="text-lg font-bold">Place Bet</Dialog.Title>
            <Dialog.Close asChild>
              <Button variant="ghost" size="icon" className="h-7 w-7 shrink-0" aria-label="Close">
                <X className="h-4 w-4" />
              </Button>
            </Dialog.Close>
          </div>

          {draft && (
            <>
              <div className="rounded-md border p-3 mb-4">
                <p className="text-sm font-medium">{draft.label}</p>
                <p className="text-xs text-muted-foreground mt-0.5">{americanOdds(draft.oddsPreview)}</p>
              </div>

              <label className="block text-xs font-medium text-muted-foreground mb-1.5">Stake ($)</label>
              <input
                type="number"
                min="1"
                step="1"
                value={stake}
                onChange={e => setStake(e.target.value)}
                className="w-full rounded-md border bg-background px-3 py-2 text-sm mb-3 focus:outline-none
                           focus:ring-2 focus:ring-ring"
              />

              <div className="flex items-center justify-between text-sm mb-4 rounded-md bg-muted p-2.5">
                <span className="text-muted-foreground">To win</span>
                <span className="font-semibold">${toWin.toFixed(2)}</span>
              </div>

              {createBet.isError && (
                <p className="text-xs text-red-600 dark:text-red-400 mb-3">
                  Could not place this bet — the line may no longer be available.
                </p>
              )}

              <Button
                className="w-full"
                disabled={stakeNum <= 0 || createBet.isPending}
                onClick={handleConfirm}
              >
                {createBet.isPending ? 'Placing…' : 'Confirm Bet'}
              </Button>
            </>
          )}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
