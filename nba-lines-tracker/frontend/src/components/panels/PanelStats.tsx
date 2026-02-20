import { Separator } from '@/components/ui/separator'
import type { TeamDetailResponse, GameLogEntry, HomeAwaySplit } from '@/types/api'
import { calcAtsPct, calcOuPct } from '@/types/api'
import { AtsCell } from '@/components/grid/AtsCell'
import { computeStreakFromGames } from '@/lib/streak'

interface PanelStatsProps {
  teamDetail: TeamDetailResponse
  games: GameLogEntry[]
}

function SplitBlock({ label, split }: { label: string; split: HomeAwaySplit }) {
  const ats = calcAtsPct(split)
  const ou = calcOuPct(split)
  return (
    <div className="flex flex-col gap-1.5">
      <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">{label}</p>
      <p className="text-sm tabular-nums">{split.wins}–{split.losses}</p>
      <div className="flex items-center gap-1">
        <span className="text-xs text-muted-foreground">ATS</span>
        <AtsCell pct={ats} />
      </div>
      <div className="flex items-center gap-1">
        <span className="text-xs text-muted-foreground">O/U</span>
        <AtsCell pct={ou} />
      </div>
    </div>
  )
}

export function PanelStats({ teamDetail, games }: PanelStatsProps) {
  // Compute overall stats from home + away splits
  const overall: HomeAwaySplit = {
    gamesPlayed: teamDetail.home.gamesPlayed + teamDetail.away.gamesPlayed,
    wins: teamDetail.home.wins + teamDetail.away.wins,
    losses: teamDetail.home.losses + teamDetail.away.losses,
    atsCovers: teamDetail.home.atsCovers + teamDetail.away.atsCovers,
    atsLosses: teamDetail.home.atsLosses + teamDetail.away.atsLosses,
    atsPushes: teamDetail.home.atsPushes + teamDetail.away.atsPushes,
    ouOvers: teamDetail.home.ouOvers + teamDetail.away.ouOvers,
    ouUnders: teamDetail.home.ouUnders + teamDetail.away.ouUnders,
    ouPushes: teamDetail.home.ouPushes + teamDetail.away.ouPushes,
  }

  // Last 10 games (games array is sorted desc by date from the API)
  const last10 = games.slice(0, 10)
  const last10Ats: HomeAwaySplit = {
    gamesPlayed: last10.length,
    wins: last10.filter(g => g.isHomeGame
      ? (g.homeScore ?? 0) > (g.awayScore ?? 0)
      : (g.awayScore ?? 0) > (g.homeScore ?? 0)).length,
    losses: last10.filter(g => g.isHomeGame
      ? (g.homeScore ?? 0) < (g.awayScore ?? 0)
      : (g.awayScore ?? 0) < (g.homeScore ?? 0)).length,
    atsCovers: last10.filter(g => g.atsResult === 'Cover').length,
    atsLosses: last10.filter(g => g.atsResult === 'Loss').length,
    atsPushes: last10.filter(g => g.atsResult === 'Push').length,
    ouOvers: last10.filter(g => g.ouResult === 'Over').length,
    ouUnders: last10.filter(g => g.ouResult === 'Under').length,
    ouPushes: last10.filter(g => g.ouResult === 'Push').length,
  }

  const streak = computeStreakFromGames(games)
  const streakText = streak ? `${streak.type}${streak.count}` : '–'
  const streakColor = streak?.type === 'W'
    ? 'text-green-600 dark:text-green-400'
    : streak?.type === 'L'
    ? 'text-red-500 dark:text-red-400'
    : 'text-muted-foreground'

  return (
    <div className="space-y-4 p-4">
      {/* Streak badge */}
      <div className="flex items-center gap-2">
        <span className="text-xs text-muted-foreground">Streak</span>
        <span className={`text-sm font-semibold tabular-nums ${streakColor}`}>{streakText}</span>
      </div>

      <Separator />

      {/* Stat blocks: Overall, Home, Away */}
      <div className="grid grid-cols-3 gap-4">
        <SplitBlock label="Overall" split={overall} />
        <SplitBlock label="Home" split={teamDetail.home} />
        <SplitBlock label="Away" split={teamDetail.away} />
      </div>

      <Separator />

      {/* Last 10 row */}
      <div className="space-y-1">
        <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">Last 10 Games</p>
        <div className="flex items-center gap-4 text-sm">
          <span className="tabular-nums text-muted-foreground">{last10Ats.wins}–{last10Ats.losses}</span>
          <div className="flex items-center gap-1">
            <span className="text-xs text-muted-foreground">ATS</span>
            <AtsCell pct={calcAtsPct(last10Ats)} />
          </div>
          <div className="flex items-center gap-1">
            <span className="text-xs text-muted-foreground">O/U</span>
            <AtsCell pct={calcOuPct(last10Ats)} />
          </div>
        </div>
      </div>
    </div>
  )
}
