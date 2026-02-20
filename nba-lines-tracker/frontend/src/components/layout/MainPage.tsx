import { useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { Moon, Sun, LogOut, RefreshCw } from 'lucide-react'
import { formatDistanceToNow } from 'date-fns'
import { Button } from '@/components/ui/button'
import { TeamGrid } from '@/components/grid/TeamGrid'
import { PanelStrip } from '@/components/panels/PanelStrip'
import { useAppStore } from '@/store/useAppStore'
import { useTeams } from '@/api/teams'
import { logout } from '@/api/auth'

export function MainPage() {
  const navigate = useNavigate()
  const theme = useAppStore(s => s.theme)
  const toggleTheme = useAppStore(s => s.toggleTheme)
  const setAuthenticated = useAppStore(s => s.setAuthenticated)
  const { data: teams = [] } = useTeams()

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
      <header className="sticky top-0 z-20 border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
        <div className="max-w-screen-xl mx-auto px-4 h-14 flex items-center gap-4">
          <h1 className="text-base font-semibold tracking-tight shrink-0">NBA Lines Tracker</h1>

          <div className="flex-1" />

          {/* Last synced indicator — GRID-06 */}
          {lastSyncedText && (
            <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <RefreshCw className="h-3 w-3" />
              <span>{lastSyncedText}</span>
            </div>
          )}

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

      {/* Main content */}
      <main className="max-w-screen-xl mx-auto px-4 py-6">
        <TeamGrid />
        <PanelStrip />
      </main>
    </div>
  )
}
