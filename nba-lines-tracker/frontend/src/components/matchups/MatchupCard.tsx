import { format } from 'date-fns'
import { useParams } from 'react-router-dom'
import { Sparkles, ListChecks } from 'lucide-react'
import { Card, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { AtsCell } from '@/components/grid/AtsCell'
import { TeamLogo } from '@/components/ui/team-logo'
import { calcAtsPct, calcOuPct } from '@/types/api'
import { useGamePreview, useGameProps } from '@/api/games'
import { useAppStore } from '@/store/useAppStore'
import { getTeamBrand } from '@/lib/teamBranding'
import type { Sport } from '@/lib/sports'
import type { TodayMatchupResponse, H2HGameEntry } from '@/types/api'

// Backend is fully built (GamePreviewService + /preview endpoint), just not exposed
// yet — deliberately holding off on Claude API cost until there's enough regular-season
// data for previews to be worth generating. Flip to true (and add Anthropic__ApiKey
// to .env) when ready.
const AI_PREVIEW_ENABLED = false

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
      <td className="py-1 pr-2 tabular-nums text-muted-foreground text-[11px]">
        {game.spreadLine !== null ? game.spreadLine : '–'}
      </td>
      <td className="py-1 pr-2 tabular-nums text-muted-foreground text-[11px]">
        {game.totalLine !== null ? game.totalLine : '–'}
      </td>
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
  const { sport } = useParams<{ sport: Sport }>()
  const { homeStats, awayStats, headToHead } = matchup
  const { data: preview, isFetching: previewLoading, isError: previewError, refetch: fetchPreview } =
    useGamePreview(matchup.gameId)
  const { data: props = [] } = useGameProps(matchup.gameId)
  const openGameProps = useAppStore(s => s.openGameProps)
  const openBetDraft = useAppStore(s => s.openBetDraft)

  const homeBrand = getTeamBrand(sport ?? 'nfl', matchup.homeTeamAbbr)
  const awayBrand = getTeamBrand(sport ?? 'nfl', matchup.awayTeamAbbr)

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
    <Card className="w-80 flex-none overflow-hidden py-0 gap-0">
      {/* Team-colored accent bar — falls back to the app's default gradient when brand
          colors aren't known (non-NFL sports, or an unmapped abbreviation) */}
      <div
        className="h-1.5 w-full"
        style={{
          background: homeBrand && awayBrand
            ? `linear-gradient(90deg, ${homeBrand.primary} 0%, ${homeBrand.secondary} 48%, ${awayBrand.secondary} 52%, ${awayBrand.primary} 100%)`
            : 'linear-gradient(90deg, var(--primary) 0%, var(--gold) 100%)',
        }}
      />
      <CardContent className="p-4 space-y-3">
        {/* Teams header */}
        <div className="flex items-center justify-between">
          <div className="text-center flex-1">
            <TeamLogo sport={sport ?? 'nfl'} abbreviation={matchup.homeTeamAbbr} className="h-10 w-10 mx-auto mb-1" />
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
            <TeamLogo sport={sport ?? 'nfl'} abbreviation={matchup.awayTeamAbbr} className="h-10 w-10 mx-auto mb-1" />
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

        {/* Spread/total bet buttons — only before kickoff, only when a line exists */}
        {matchup.status === 'SCHEDULED' && (matchup.spreadLine !== null || matchup.totalLine !== null) && (
          <div className="flex flex-wrap justify-center gap-1.5 text-[11px]">
            {matchup.spreadLine !== null && (
              <>
                <button
                  onClick={() => openBetDraft({
                    gameId: matchup.gameId,
                    kind: 'Spread',
                    side: 'Home',
                    label: `${matchup.homeTeamAbbr} ${matchup.favoriteTeamId === matchup.homeTeamId ? '-' : '+'}${matchup.spreadLine}`,
                    oddsPreview: matchup.homeSpreadOdds ?? -110,
                  })}
                  className="rounded border px-2 py-1 tabular-nums hover:bg-accent"
                >
                  {matchup.homeTeamAbbr} {matchup.favoriteTeamId === matchup.homeTeamId ? '-' : '+'}{matchup.spreadLine}
                </button>
                <button
                  onClick={() => openBetDraft({
                    gameId: matchup.gameId,
                    kind: 'Spread',
                    side: 'Away',
                    label: `${matchup.awayTeamAbbr} ${matchup.favoriteTeamId === matchup.awayTeamId ? '-' : '+'}${matchup.spreadLine}`,
                    oddsPreview: matchup.awaySpreadOdds ?? -110,
                  })}
                  className="rounded border px-2 py-1 tabular-nums hover:bg-accent"
                >
                  {matchup.awayTeamAbbr} {matchup.favoriteTeamId === matchup.awayTeamId ? '-' : '+'}{matchup.spreadLine}
                </button>
              </>
            )}
            {matchup.totalLine !== null && (
              <>
                <button
                  onClick={() => openBetDraft({
                    gameId: matchup.gameId,
                    kind: 'Total',
                    side: 'Over',
                    label: `Total Over ${matchup.totalLine}`,
                    oddsPreview: matchup.overOdds ?? -110,
                  })}
                  className="rounded border px-2 py-1 tabular-nums hover:bg-accent"
                >
                  O {matchup.totalLine}
                </button>
                <button
                  onClick={() => openBetDraft({
                    gameId: matchup.gameId,
                    kind: 'Total',
                    side: 'Under',
                    label: `Total Under ${matchup.totalLine}`,
                    oddsPreview: matchup.underOdds ?? -110,
                  })}
                  className="rounded border px-2 py-1 tabular-nums hover:bg-accent"
                >
                  U {matchup.totalLine}
                </button>
              </>
            )}
          </div>
        )}

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
                  <th className="text-left text-[10px] font-medium pb-1 pr-2">Sprd</th>
                  <th className="text-left text-[10px] font-medium pb-1 pr-2">Tot</th>
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

        {/* Player props — opens the category browser; no leaderboard detour needed */}
        {props.length > 0 && (
          <>
            <Separator />
            <Button
              variant="outline"
              size="sm"
              className="h-8 w-full text-xs"
              onClick={() => openGameProps({
                id: matchup.gameId,
                title: `${matchup.awayTeamAbbr} @ ${matchup.homeTeamAbbr}`,
              })}
            >
              <ListChecks className="h-3.5 w-3.5 mr-1.5" />
              Player Props ({props.length})
            </Button>
          </>
        )}

        {AI_PREVIEW_ENABLED && <Separator />}

        {/* AI preview — generated on demand, never automatically */}
        <div className={AI_PREVIEW_ENABLED ? undefined : 'hidden'}>
          {!preview && (
            <Button
              variant="outline"
              size="sm"
              className="h-7 w-full text-xs"
              disabled={previewLoading}
              onClick={() => fetchPreview()}
            >
              <Sparkles className="h-3.5 w-3.5 mr-1.5" />
              {previewLoading ? 'Generating preview…' : 'AI Preview'}
            </Button>
          )}

          {previewError && (
            <p className="text-xs text-muted-foreground italic mt-1">
              Could not generate a preview right now.
            </p>
          )}

          {preview && (
            <div className="space-y-1.5">
              <p className="text-[10px] font-medium text-muted-foreground uppercase tracking-wider flex items-center gap-1">
                <Sparkles className="h-3 w-3" /> AI Preview
              </p>
              <p className="text-xs leading-relaxed whitespace-pre-line">{preview.text}</p>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  )
}
