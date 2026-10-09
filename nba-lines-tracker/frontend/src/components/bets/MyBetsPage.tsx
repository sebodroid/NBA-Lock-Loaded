import { useMemo, useState } from 'react'
import { Trash2, PenLine, Plus } from 'lucide-react'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useBets, useDeleteBet } from '@/api/bets'
import { BankrollChart } from './BankrollChart'
import { ManualBetModal } from './ManualBetModal'
import type { BetResponse } from '@/types/api'

function americanOdds(v: number): string {
  return v > 0 ? `+${v}` : `${v}`
}

function outcomeBadgeClass(outcome: string): string {
  if (outcome === 'Won')  return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400 border-0'
  if (outcome === 'Lost') return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400 border-0'
  if (outcome === 'Push') return 'bg-muted text-muted-foreground border-0'
  return 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400 border-0'   // Pending
}

function selectionText(bet: BetResponse): string {
  if (bet.kind === 'PlayerProp') return `${bet.playerName} ${bet.side} ${bet.lineAtBet} ${bet.marketLabel}`
  if (bet.kind === 'Total') return `Total ${bet.side} ${bet.lineAtBet}`
  // Spread — lineAtBet is signed from the picked team's own perspective
  const sign = bet.lineAtBet >= 0 ? '+' : ''
  return `${bet.teamAbbreviation} ${sign}${bet.lineAtBet}`
}

function clvColorClass(pct: number): string {
  if (pct > 0) return 'text-green-600 dark:text-green-400'
  if (pct < 0) return 'text-red-600 dark:text-red-400'
  return 'text-muted-foreground'
}

export function MyBetsPage() {
  const { data: bets = [], isLoading, isError } = useBets()
  const deleteBet = useDeleteBet()
  const [manualBetOpen, setManualBetOpen] = useState(false)

  const summary = useMemo(() => {
    const won = bets.filter(b => b.outcome === 'Won')
    const lost = bets.filter(b => b.outcome === 'Lost')
    const pending = bets.filter(b => b.outcome === 'Pending')
    const totalStaked = bets.reduce((sum, b) => sum + b.stakeAmount, 0)
    const netProfit = won.reduce((sum, b) => sum + b.toWinAmount, 0) - lost.reduce((sum, b) => sum + b.stakeAmount, 0)

    const withClv = bets.filter((b): b is BetResponse & { clvPct: number } => b.clvPct !== null)
    const avgClv = withClv.length > 0
      ? withClv.reduce((sum, b) => sum + b.clvPct, 0) / withClv.length
      : null

    return {
      wonCount: won.length, lostCount: lost.length, pendingCount: pending.length,
      totalStaked, netProfit, avgClv,
    }
  }, [bets])

  return (
    <div>
      <div className="flex items-center justify-between mb-3">
        <p className="text-sm text-muted-foreground">Every bet you've placed, tracked and graded automatically.</p>
        <Button size="sm" className="h-8 text-xs" onClick={() => setManualBetOpen(true)}>
          <Plus className="h-3.5 w-3.5 mr-1.5" />
          Log a Bet
        </Button>
      </div>

      <div className="grid grid-cols-2 sm:grid-cols-5 gap-3 mb-4">
        <div className="rounded-md border p-3">
          <p className="text-xs text-muted-foreground">Record</p>
          <p className="text-lg font-semibold tabular-nums">{summary.wonCount}-{summary.lostCount}</p>
        </div>
        <div className="rounded-md border p-3">
          <p className="text-xs text-muted-foreground">Pending</p>
          <p className="text-lg font-semibold tabular-nums">{summary.pendingCount}</p>
        </div>
        <div className="rounded-md border p-3">
          <p className="text-xs text-muted-foreground">Total Staked</p>
          <p className="text-lg font-semibold tabular-nums">${summary.totalStaked.toFixed(2)}</p>
        </div>
        <div className="rounded-md border p-3">
          <p className="text-xs text-muted-foreground">Net Profit</p>
          <p className={`text-lg font-semibold tabular-nums ${summary.netProfit > 0 ? 'text-green-600 dark:text-green-400' : summary.netProfit < 0 ? 'text-red-600 dark:text-red-400' : ''}`}>
            {summary.netProfit >= 0 ? '+' : ''}${summary.netProfit.toFixed(2)}
          </p>
        </div>
        <div className="rounded-md border p-3" title="Average closing-line value across bets where the line has closed — positive means you're beating the market on average">
          <p className="text-xs text-muted-foreground">Avg CLV</p>
          <p className={`text-lg font-semibold tabular-nums ${summary.avgClv === null ? '' : clvColorClass(summary.avgClv)}`}>
            {summary.avgClv === null ? '–' : `${summary.avgClv >= 0 ? '+' : ''}${summary.avgClv.toFixed(1)}%`}
          </p>
        </div>
      </div>

      {isLoading && (
        <div className="space-y-2">
          {[0, 1, 2].map(i => <Skeleton key={i} className="h-10 w-full" />)}
        </div>
      )}

      {isError && <p className="text-sm text-muted-foreground italic">Could not load your bets.</p>}

      {!isLoading && !isError && bets.length > 0 && (
        <div className="rounded-md border p-4 mb-4">
          <BankrollChart bets={bets} />
        </div>
      )}

      {!isLoading && !isError && bets.length === 0 && (
        <p className="text-sm text-muted-foreground italic">
          No bets tracked yet — place one from a game's Player Props or spread/total buttons,
          or log one you placed elsewhere with "Log a Bet" above.
        </p>
      )}

      {!isLoading && !isError && bets.length > 0 && (
        <div className="rounded-md border overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow className="hover:bg-transparent">
                <TableHead className="h-10 text-xs font-medium">Game</TableHead>
                <TableHead className="h-10 text-xs font-medium">Bet</TableHead>
                <TableHead className="h-10 text-xs font-medium">Odds</TableHead>
                <TableHead className="h-10 text-xs font-medium">Stake</TableHead>
                <TableHead className="h-10 text-xs font-medium">To Win</TableHead>
                <TableHead className="h-10 text-xs font-medium" title="Closing-line value — did the market move toward or away from your bet after you placed it?">
                  CLV
                </TableHead>
                <TableHead className="h-10 text-xs font-medium">Result</TableHead>
                <TableHead className="h-10 text-xs font-medium"></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {bets.map(bet => (
                <TableRow key={bet.id}>
                  <TableCell className="py-2.5 text-sm whitespace-nowrap">
                    <div>{bet.gameLabel}</div>
                    <div className="text-xs text-muted-foreground">{bet.gameDate}</div>
                  </TableCell>
                  <TableCell className="py-2.5 text-sm whitespace-nowrap">
                    <span className="inline-flex items-center gap-1.5">
                      {selectionText(bet)}
                      {bet.isManual && (
                        <PenLine className="h-3 w-3 text-muted-foreground shrink-0" aria-label="Manually logged" />
                      )}
                    </span>
                  </TableCell>
                  <TableCell className="py-2.5 text-sm tabular-nums text-muted-foreground whitespace-nowrap">
                    {americanOdds(bet.oddsAtBet)}
                  </TableCell>
                  <TableCell className="py-2.5 text-sm tabular-nums whitespace-nowrap">
                    ${bet.stakeAmount.toFixed(2)}
                  </TableCell>
                  <TableCell className="py-2.5 text-sm tabular-nums text-muted-foreground whitespace-nowrap">
                    ${bet.toWinAmount.toFixed(2)}
                  </TableCell>
                  <TableCell
                    className={`py-2.5 text-sm tabular-nums whitespace-nowrap ${bet.clvPct === null ? 'text-muted-foreground' : clvColorClass(bet.clvPct)}`}
                    title={bet.closingLine !== null ? `Closing line: ${bet.closingLine} (${bet.closingOdds !== null ? americanOdds(bet.closingOdds) : '–'})` : undefined}
                  >
                    {bet.clvPct === null ? '–' : `${bet.clvPct >= 0 ? '+' : ''}${bet.clvPct.toFixed(1)}%`}
                  </TableCell>
                  <TableCell className="py-2.5 text-sm whitespace-nowrap">
                    <Badge variant="outline" className={`text-[10px] ${outcomeBadgeClass(bet.outcome)}`}>
                      {bet.outcome}
                    </Badge>
                    {bet.actualValue !== null && (
                      <span className="ml-1.5 text-xs text-muted-foreground tabular-nums">
                        (actual: {bet.actualValue})
                      </span>
                    )}
                  </TableCell>
                  <TableCell className="py-2.5 text-right">
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-7 w-7 text-muted-foreground hover:text-red-600"
                      onClick={() => deleteBet.mutate(bet.id)}
                      aria-label="Remove bet"
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <ManualBetModal open={manualBetOpen} onClose={() => setManualBetOpen(false)} />
    </div>
  )
}
