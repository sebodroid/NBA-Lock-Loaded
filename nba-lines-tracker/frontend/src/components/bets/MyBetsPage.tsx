import { useMemo } from 'react'
import { Trash2 } from 'lucide-react'
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
  if (bet.kind === 'PlayerProp') return `${bet.playerName} ${bet.side} ${bet.lineAtBet}`
  if (bet.kind === 'Total') return `Total ${bet.side} ${bet.lineAtBet}`
  // Spread — lineAtBet is signed from the picked team's own perspective
  const sign = bet.lineAtBet >= 0 ? '+' : ''
  return `${bet.teamAbbreviation} ${sign}${bet.lineAtBet}`
}

export function MyBetsPage() {
  const { data: bets = [], isLoading, isError } = useBets()
  const deleteBet = useDeleteBet()

  const summary = useMemo(() => {
    const won = bets.filter(b => b.outcome === 'Won')
    const lost = bets.filter(b => b.outcome === 'Lost')
    const pending = bets.filter(b => b.outcome === 'Pending')
    const totalStaked = bets.reduce((sum, b) => sum + b.stakeAmount, 0)
    const netProfit = won.reduce((sum, b) => sum + b.toWinAmount, 0) - lost.reduce((sum, b) => sum + b.stakeAmount, 0)
    return { wonCount: won.length, lostCount: lost.length, pendingCount: pending.length, totalStaked, netProfit }
  }, [bets])

  return (
    <div>
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-4">
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
      </div>

      {isLoading && (
        <div className="space-y-2">
          {[0, 1, 2].map(i => <Skeleton key={i} className="h-10 w-full" />)}
        </div>
      )}

      {isError && <p className="text-sm text-muted-foreground italic">Could not load your bets.</p>}

      {!isLoading && !isError && bets.length === 0 && (
        <p className="text-sm text-muted-foreground italic">
          No bets tracked yet — place one from a game's Player Props or spread/total buttons.
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
                  <TableCell className="py-2.5 text-sm whitespace-nowrap">{selectionText(bet)}</TableCell>
                  <TableCell className="py-2.5 text-sm tabular-nums text-muted-foreground whitespace-nowrap">
                    {americanOdds(bet.oddsAtBet)}
                  </TableCell>
                  <TableCell className="py-2.5 text-sm tabular-nums whitespace-nowrap">
                    ${bet.stakeAmount.toFixed(2)}
                  </TableCell>
                  <TableCell className="py-2.5 text-sm tabular-nums text-muted-foreground whitespace-nowrap">
                    ${bet.toWinAmount.toFixed(2)}
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
    </div>
  )
}
