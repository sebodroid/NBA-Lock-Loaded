import { useCallback, useState } from 'react'
import { useNavigate, useParams, Link } from 'react-router-dom'
import { Moon, Sun, LogOut, RefreshCw } from 'lucide-react'
import { formatDistanceToNow } from 'date-fns'
import { Button } from '@/components/ui/button'
import { TeamGrid } from '@/components/grid/TeamGrid'
import { PanelStrip } from '@/components/panels/PanelStrip'
import { MatchupsSection } from '@/components/matchups/MatchupsSection'
import { LeaderboardSection } from '@/components/leaderboard/LeaderboardSection'
import { InsightsSection } from '@/components/insights/InsightsSection'
import { PlayerCardModal } from '@/components/players/PlayerCardModal'
import { GamePropsModal } from '@/components/matchups/GamePropsModal'
import { PlaceBetModal } from '@/components/bets/PlaceBetModal'
import { MyBetsPage } from '@/components/bets/MyBetsPage'
import { HotBetsPage } from '@/components/bets/HotBetsPage'
import { PaletteMenu } from '@/components/layout/PaletteMenu'
import { useAppStore } from '@/store/useAppStore'
import { useTeams } from '@/api/teams'
import { logout } from '@/api/auth'
import { SPORTS, SPORT_LABELS, type Sport } from '@/lib/sports'
import { cn } from '@/lib/utils'

const VIEWS = [
  { key: 'dashboard', label: 'Dashboard' },
  { key: 'bets', label: 'My Bets' },
  { key: 'hot', label: 'Hot Bets' },
] as const
type View = typeof VIEWS[number]['key']

export function MainPage() {
  const navigate = useNavigate()
  const { sport } = useParams<{ sport: Sport }>()
  const theme = useAppStore(s => s.theme)
  const toggleTheme = useAppStore(s => s.toggleTheme)
  const setAuthenticated = useAppStore(s => s.setAuthenticated)
  const { data: teams = [] } = useTeams()
  const [view, setView] = useState<View>('dashboard')

  const lastSyncedAt = teams[0]?.lastSyncedAt ?? null
  const lastSyncedText = lastSyncedAt
    ? `Last synced: ${formatDistanceToNow(new Date(lastSyncedAt), { addSuffix: true })}`
    : null

  const handleLogout = useCallback(async () => {
    await logout()
    setAuthenticated(false)
    navigate('/login', { replace: true })
  }, [setAuthenticated, navigate])

  return (
    <div className="min-h-screen bg-background">
      {/* Header */}
      <header className="sticky top-0 z-20 border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60
                          bg-gradient-to-r from-primary/10 via-transparent to-gold/10">
        <div className="max-w-screen-xl mx-auto px-4 h-14 flex items-center gap-4">
          <h1 className="text-base font-bold tracking-tight shrink-0">
            Bet<span className="text-primary">The</span><span className="text-gold">House</span>
          </h1>

          {/* Sport switcher */}
          <nav className="flex items-center gap-1">
            {SPORTS.map(s => (
              <Link
                key={s}
                to={`/${s}`}
                className={cn(
                  'rounded-md px-2.5 py-1 text-xs font-medium transition-colors',
                  s === sport
                    ? 'bg-primary text-primary-foreground'
                    : 'text-muted-foreground hover:bg-muted hover:text-foreground'
                )}
              >
                {SPORT_LABELS[s]}
              </Link>
            ))}
          </nav>

          <div className="flex-1" />

          {/* Last synced indicator — GRID-06 */}
          {lastSyncedText && (
            <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <RefreshCw className="h-3 w-3" />
              <span>{lastSyncedText}</span>
            </div>
          )}

          {/* Palette picker */}
          <PaletteMenu />

          {/* Theme toggle */}
          <Button
            variant="ghost"
            size="icon"
            className="h-8 w-8"
            onClick={toggleTheme}
            aria-label="Toggle theme"
          >
            {theme === 'dark'
              ? <Sun className="h-4 w-4" />
              : <Moon className="h-4 w-4" />}
          </Button>

          {/* Logout */}
          <Button
            variant="ghost"
            size="sm"
            className="h-8 text-xs text-muted-foreground hover:text-foreground"
            onClick={handleLogout}
          >
            <LogOut className="h-3.5 w-3.5 mr-1.5" />
            Sign out
          </Button>
        </div>
      </header>

      {/* View tabs — Dashboard is everything that was already here; My Bets / Hot Bets
          are separate pages within the same sport, not separate routes. */}
      <div className="border-b bg-background/95">
        <div className="max-w-screen-xl mx-auto px-4">
          <nav className="flex items-center gap-1 py-2">
            {VIEWS.map(v => (
              <button
                key={v.key}
                onClick={() => setView(v.key)}
                className={cn(
                  'rounded-md px-2.5 py-1 text-xs font-medium transition-colors',
                  v.key === view
                    ? 'bg-primary text-primary-foreground'
                    : 'text-muted-foreground hover:bg-muted hover:text-foreground'
                )}
              >
                {v.label}
              </button>
            ))}
          </nav>
        </div>
      </div>

      {/* Main content */}
      <main className="max-w-screen-xl mx-auto px-4 py-6">
        {view === 'dashboard' && (
          <>
            <MatchupsSection />
            <InsightsSection />
            <TeamGrid />
            {/* Player-level stats only exist for NFL right now — hide for NBA/MLB rather
                than show an empty/misleading leaderboard until those sports get the same
                player-stat pipeline. */}
            {sport === 'nfl' && <LeaderboardSection />}
            <PanelStrip />
          </>
        )}
        {view === 'bets' && <MyBetsPage />}
        {view === 'hot' && <HotBetsPage />}
      </main>

      <PlayerCardModal />
      <GamePropsModal />
      <PlaceBetModal />
    </div>
  )
}
