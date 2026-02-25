import { format } from 'date-fns'
import { Card, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { AtsCell } from '@/components/grid/AtsCell'
import { calcAtsPct, calcOuPct } from '@/types/api'
import type { TodayMatchupResponse, H2HGameEntry } from '@/types/api'

interface MatchupCardProps {
  matchup: TodayMatchupResponse
}

function atsBadgeClass(result: string | null): string {
  if (result === 'Cover') return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400 border-0'
  if (result === 'Loss')  return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400 border-0'
  return 'bg-muted text-muted-foreground border-0'
}

function ouBadgeClass(result: string | null): string {
  if (result === 'Over')  return 'bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-400 border-0'
  if (result === 'Under') return 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400 border-0'
  return 'bg-muted text-muted-foreground border-0'
}

function statusBadgeClass(status: string): string {
  if (status === 'LIVE')  return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400 border-0'
  if (status === 'FINAL') return 'bg-muted text-muted-foreground border-0'
  return 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400 border-0'
}

function H2HRow({ game, homeTeamId }: { game: H2HGameEntry; homeTeamId: number }) {
  const isHomeTeamHome = game.homeTeamId === homeTeamId
  const homeScore = game.homeScore
  const awayScore = game.awayScore

  // Score display: show from "home team of this matchup" perspective
  const scoreText = homeScore !== null && awayScore !== null
    ? (isHomeTeamHome ? `${homeScore}–${awayScore}` : `${awayScore}–${homeScore}`)
    : '–'

  // ATS result from home team (of this card's matchup) perspective
  const atsResult = isHomeTeamHome ? game.homeAtsResult : game.awayAtsResult
  const ouResult = game.ouResult

  return (
    <tr className="border-b border-border/50">
      <td className="py-1 pr-2 tabular-nums text-muted-foreground text-[11px]">
        {format(new Date(game.gameDate + 'T00:00:00'), 'M/d')}
      </td>
      <td className="py-1 pr-2 tabular-nums text-[11px]">{scoreText}</td>
      <td className="py-1 pr-2">
        {atsResult ? (
          <Badge variant="outline" className={`text-[10px] px-1 py-0 ${atsBadgeClass(atsResult)}`}>
            {atsResult}
          </Badge>
        ) : <span className="text-muted-foreground text-[11px]">–</span>}
      </td>
      <td className="py-1">
        {ouResult ? (
          <Badge variant="outline" className={`text-[10px] px-1 py-0 ${ouBadgeClass(ouResult)}`}>
            {ouResult}
          </Badge>
        ) : <span className="text-muted-foreground text-[11px]">–</span>}
      </td>
    </tr>
  )
}

export function MatchupCard({ matchup }: MatchupCardProps) {
  const { homeStats, awayStats, headToHead } = matchup

  const homeAtsPct = calcAtsPct(homeStats)
  const homeOuPct  = calcOuPct(homeStats)
  const awayAtsPct = calcAtsPct(awayStats)
  const awayOuPct  = calcOuPct(awayStats)

  const favTeamAbbr = matchup.favoriteTeamId === matchup.homeTeamId
    ? matchup.homeTeamAbbr
    : matchup.favoriteTeamId === matchup.awayTeamId
    ? matchup.awayTeamAbbr
    : null

  const spreadText = matchup.spreadLine !== null && favTeamAbbr
    ? `${favTeamAbbr} -${matchup.spreadLine}`
    : matchup.spreadLine !== null
    ? `${matchup.spreadLine}`
    : 'No line'

  const totalText = matchup.totalLine !== null ? `O/U ${matchup.totalLine}` : 'No total'

  return (
    <Card className="w-80 flex-none">
      <CardContent className="p-4 space-y-3">
        {/* Teams header */}
        <div className="flex items-center justify-between">
          <div className="text-center flex-1">
            <p className="text-lg font-bold">{matchup.homeTeamAbbr}</p>
            <p className="text-xs text-muted-foreground truncate">{matchup.homeTeamName}</p>
          </div>
          <div className="text-center px-2">
            <p className="text-xs text-muted-foreground font-medium">vs</p>
            <Badge variant="outline" className={`text-[10px] mt-1 ${statusBadgeClass(matchup.status)}`}>
              {matchup.status}
            </Badge>
          </div>
          <div className="text-center flex-1">
            <p className="text-lg font-bold">{matchup.awayTeamAbbr}</p>
            <p className="text-xs text-muted-foreground truncate">{matchup.awayTeamName}</p>
          </div>
        </div>

        {/* Line */}
        <div className="flex justify-center gap-4 text-xs text-muted-foreground">
          <span>{spreadText}</span>
          <span>·</span>
          <span>{totalText}</span>
        </div>

        <Separator />

        {/* Season stats comparison */}
        <div>
          <p className="text-[10px] font-medium text-muted-foreground uppercase tracking-wider mb-2">Season</p>
          <table className="w-full text-xs">
            <thead>
              <tr className="text-muted-foreground">
                <th className="text-left font-medium pb-1"></th>
                <th className="text-center font-medium pb-1">{matchup.homeTeamAbbr}</th>
                <th className="text-center font-medium pb-1">{matchup.awayTeamAbbr}</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td className="py-0.5 text-muted-foreground">W–L</td>
                <td className="py-0.5 text-center tabular-nums">{homeStats.wins}–{homeStats.losses}</td>
                <td className="py-0.5 text-center tabular-nums">{awayStats.wins}–{awayStats.losses}</td>
              </tr>
              <tr>
                <td className="py-0.5 text-muted-foreground">ATS%</td>
                <td className="py-0.5 text-center"><AtsCell pct={homeAtsPct} /></td>
                <td className="py-0.5 text-center"><AtsCell pct={awayAtsPct} /></td>
              </tr>
              <tr>
                <td className="py-0.5 text-muted-foreground">O/U%</td>
                <td className="py-0.5 text-center"><AtsCell pct={homeOuPct} /></td>
                <td className="py-0.5 text-center"><AtsCell pct={awayOuPct} /></td>
              </tr>
            </tbody>
          </table>
        </div>

        <Separator />

        {/* Head-to-head history */}
        <div>
          <p className="text-[10px] font-medium text-muted-foreground uppercase tracking-wider mb-2">
            Head to Head (2025–26)
          </p>
          {headToHead.length === 0 ? (
            <p className="text-xs text-muted-foreground italic">No matchups yet this season</p>
          ) : (
            <table className="w-full">
              <thead>
                <tr className="text-muted-foreground">
                  <th className="text-left text-[10px] font-medium pb-1 pr-2">Date</th>
                  <th className="text-left text-[10px] font-medium pb-1 pr-2">Score</th>
                  <th className="text-left text-[10px] font-medium pb-1 pr-2">ATS</th>
                  <th className="text-left text-[10px] font-medium pb-1">O/U</th>
                </tr>
              </thead>
              <tbody>
                {headToHead.map(g => (
                  <H2HRow key={g.gameId} game={g} homeTeamId={matchup.homeTeamId} />
                ))}
              </tbody>
            </table>
          )}
        </div>
      </CardContent>
    </Card>
  )
}
