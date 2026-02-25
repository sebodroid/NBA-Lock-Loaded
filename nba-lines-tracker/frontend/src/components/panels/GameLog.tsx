import { Badge } from '@/components/ui/badge'
import { format } from 'date-fns'
import type { GameLogEntry } from '@/types/api'

interface GameLogProps {
  games: GameLogEntry[]
  teamAbbr: string
}

function atsBadgeClass(result: string | null): string {
  if (result === 'Cover') return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400 border-0'
  if (result === 'Loss') return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400 border-0'
  return 'bg-muted text-muted-foreground border-0'
}

function ouBadgeClass(result: string | null): string {
  if (result === 'Over') return 'bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-400 border-0'
  if (result === 'Under') return 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400 border-0'
  return 'bg-muted text-muted-foreground border-0'
}

export function GameLog({ games, teamAbbr }: GameLogProps) {
  void teamAbbr  // reserved for future use (e.g. highlighting team's score column)
  const display = games.slice(0, 25)  // cap at 25 rows for panel height

  return (
    <div className="px-4 pb-4">
      <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-2">Game Log</p>
      <div className="overflow-x-auto">
        <table className="w-full text-xs border-collapse">
          <thead>
            <tr className="border-b text-muted-foreground">
              <th className="text-left py-1.5 pr-3 font-medium">Date</th>
              <th className="text-left py-1.5 pr-3 font-medium">Opponent</th>
              <th className="text-left py-1.5 pr-3 font-medium">Score</th>
              <th className="text-left py-1.5 pr-3 font-medium">Spread</th>
              <th className="text-left py-1.5 pr-3 font-medium">ATS</th>
              <th className="text-left py-1.5 pr-3 font-medium">Total</th>
              <th className="text-left py-1.5 font-medium">O/U</th>
            </tr>
          </thead>
          <tbody>
            {display.map(g => {
              const opponent = g.isHomeGame ? g.awayTeamAbbr : g.homeTeamAbbr
              const teamScore = g.isHomeGame ? g.homeScore : g.awayScore
              const oppScore = g.isHomeGame ? g.awayScore : g.homeScore
              const scoreText = teamScore !== null && oppScore !== null
                ? `${teamScore}–${oppScore}`
                : '–'
              const spreadText = g.spreadLine !== null ? `${g.spreadLine > 0 ? '+' : ''}${g.spreadLine}` : '–'
              const totalText = g.totalLine !== null ? g.totalLine.toString() : '–'

              return (
                <tr key={g.gameId} className="border-b border-border/50 hover:bg-muted/30">
                  <td className="py-1.5 pr-3 tabular-nums text-muted-foreground">
                    {format(new Date(g.gameDate + 'T00:00:00'), 'M/d')}
                  </td>
                  <td className="py-1.5 pr-3 font-medium">
                    {g.isHomeGame ? '' : '@'}{opponent}
                  </td>
                  <td className="py-1.5 pr-3 tabular-nums">{scoreText}</td>
                  <td className="py-1.5 pr-3 tabular-nums text-muted-foreground">{spreadText}</td>
                  <td className="py-1.5 pr-3">
                    {g.atsResult ? (
                      <Badge variant="outline" className={`text-[10px] px-1 py-0 ${atsBadgeClass(g.atsResult)}`}>
                        {g.atsResult}
                      </Badge>
                    ) : <span className="text-muted-foreground">–</span>}
                  </td>
                  <td className="py-1.5 pr-3 tabular-nums text-muted-foreground">{totalText}</td>
                  <td className="py-1.5">
                    {g.ouResult ? (
                      <Badge variant="outline" className={`text-[10px] px-1 py-0 ${ouBadgeClass(g.ouResult)}`}>
                        {g.ouResult}
                      </Badge>
                    ) : <span className="text-muted-foreground">–</span>}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
        {games.length === 0 && (
          <p className="text-center text-muted-foreground py-6 text-sm">No game data available.</p>
        )}
      </div>
    </div>
  )
}
