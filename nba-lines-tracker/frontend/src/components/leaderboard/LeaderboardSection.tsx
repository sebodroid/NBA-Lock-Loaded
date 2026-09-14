import { useMemo, useState } from 'react'
import { ArrowUpDown, ChevronDown, Check, SlidersHorizontal } from 'lucide-react'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { SimpleDropdown } from '@/components/ui/simple-dropdown'
import { CollapsibleSection } from '@/components/ui/collapsible-section'
import { TeamLogo } from '@/components/ui/team-logo'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useLeaderboard } from '@/api/players'
import { useTeams } from '@/api/teams'
import { useAppStore } from '@/store/useAppStore'
import { useLocalStorage } from '@/hooks/useLocalStorage'
import { cn } from '@/lib/utils'

const CATEGORIES = ['Passing', 'Rushing', 'Receiving', 'Defense'] as const
type Category = typeof CATEGORIES[number]
const ALL_TEAMS = 'all'

// Only NFL has a historical season backfilled right now — this section is already
// NFL-only (gated in MainPage), so no per-sport check needed here.
const HISTORICAL_SEASONS = ['2025']

export function LeaderboardSection() {
  const openPlayerCard = useAppStore(s => s.openPlayerCard)
  const [category, setCategory] = useState<Category>('Passing')
  const [season, setSeason] = useState<string | undefined>(undefined)   // undefined = live current season
  const [teamFilter, setTeamFilter] = useState(ALL_TEAMS)
  const [sortKey, setSortKey] = useState<string | null>(null)
  const [sortDesc, setSortDesc] = useState(true)
  const { data: allPlayers = [], isLoading, isError } = useLeaderboard(category, season)
  const { data: teams = [] } = useTeams()

  const players = useMemo(() => {
    if (teamFilter === ALL_TEAMS) return allPlayers
    return allPlayers.filter(p => p.teamAbbreviation === teamFilter)
  }, [allPlayers, teamFilter])

  // Stat columns are derived from whatever the API actually returned — never hardcoded,
  // since the exact stat-name vocabulary the data source uses isn't fully known yet.
  // This also means the column list is naturally scoped to the selected category already
  // (a Rushing query never returns passing stats), so "hide columns that don't apply to
  // this player type" falls out for free — the toggle below just narrows within that.
  const statColumns = useMemo(() => {
    const keys = new Set<string>()
    for (const p of players) {
      for (const k of Object.keys(p.stats)) keys.add(k)
    }
    return Array.from(keys).sort()
  }, [players])

  // Hidden columns and display order, both kept separately per category — switching
  // from Passing to Rushing shouldn't carry over a column selection (or ordering) that
  // doesn't even exist there.
  const [hiddenByCategory, setHiddenByCategory] = useLocalStorage<Record<string, string[]>>(
    'nfl-leaderboard-hidden-columns',
    {}
  )
  // No drag-and-drop (would need a new dependency for a "nice to have") — instead,
  // clicking columns in the dropdown builds this order list in click sequence: the
  // first one you click lands leftmost, the second right after it, and so on.
  const [orderByCategory, setOrderByCategory] = useLocalStorage<Record<string, string[]>>(
    'nfl-leaderboard-column-order',
    {}
  )

  const hiddenSet = useMemo(() => new Set(hiddenByCategory[category] ?? []), [hiddenByCategory, category])

  // Every column that exists for this category, ranked front-to-back in the order they
  // were clicked (untouched columns fall in alphabetically after) — the same order the
  // dropdown list and the visible table columns both use, so what you see in one matches
  // the other.
  const rankedColumns = useMemo(() => {
    const order = orderByCategory[category] ?? []
    return [...order, ...statColumns.filter(c => !order.includes(c))].filter(c => statColumns.includes(c))
  }, [statColumns, orderByCategory, category])

  const visibleStatColumns = useMemo(
    () => rankedColumns.filter(c => !hiddenSet.has(c)),
    [rankedColumns, hiddenSet]
  )

  function toggleColumn(col: string) {
    setHiddenByCategory(prev => {
      const current = new Set(prev[category] ?? [])
      if (current.has(col)) current.delete(col)
      else current.add(col)
      return { ...prev, [category]: Array.from(current) }
    })
    // Append to the back of the click order regardless of which direction the toggle
    // went (harmless when hiding — order only matters once it's visible again). First
    // column clicked lands leftmost, second click lands right after it, and so on —
    // click order reads left-to-right, same as the order you clicked in.
    setOrderByCategory(prev => ({
      ...prev,
      [category]: [...(prev[category] ?? []).filter(c => c !== col), col],
    }))
  }

  function selectAllColumns() {
    setHiddenByCategory(prev => ({ ...prev, [category]: [] }))
  }

  function deselectAllColumns() {
    setHiddenByCategory(prev => ({ ...prev, [category]: [...statColumns] }))
  }

  const sorted = useMemo(() => {
    if (!sortKey) return players
    return [...players].sort((a, b) => {
      const av = a.stats[sortKey] ?? -Infinity
      const bv = b.stats[sortKey] ?? -Infinity
      return sortDesc ? bv - av : av - bv
    })
  }, [players, sortKey, sortDesc])

  const selectedTeamLabel = teamFilter === ALL_TEAMS
    ? 'All Teams'
    : teams.find(t => t.abbreviation === teamFilter)?.name ?? teamFilter

  function toggleSort(key: string) {
    if (sortKey === key) {
      setSortDesc(d => !d)
    } else {
      setSortKey(key)
      setSortDesc(true)
    }
  }

  function selectCategory(c: Category) {
    setCategory(c)
    setSortKey(null)
  }

  return (
    <CollapsibleSection title="Leaderboards">
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-1">
          <Button
            variant={season === undefined ? 'default' : 'outline'}
            size="sm"
            onClick={() => setSeason(undefined)}
            className="h-7 px-2.5 text-xs"
          >
            Current
          </Button>
          {HISTORICAL_SEASONS.map(s => (
            <Button
              key={s}
              variant={season === s ? 'default' : 'outline'}
              size="sm"
              onClick={() => setSeason(s)}
              className="h-7 px-2.5 text-xs"
            >
              {s}
            </Button>
          ))}
        </div>

        <div className="flex items-center gap-2">
          <div className="flex items-center gap-1">
            {CATEGORIES.map(c => (
              <Button
                key={c}
                variant={category === c ? 'default' : 'outline'}
                size="sm"
                onClick={() => selectCategory(c)}
                className="h-8 px-3 text-xs"
              >
                {c}
              </Button>
            ))}
          </div>

          <SimpleDropdown
            align="end"
            panelClassName="w-48 max-h-72 overflow-y-auto"
            trigger={
              <Button variant="outline" size="sm" className="h-8 px-3 text-xs">
                {selectedTeamLabel}
                <ChevronDown className="ml-1.5 h-3.5 w-3.5" />
              </Button>
            }
          >
            {close => (
              <>
                <button
                  type="button"
                  className="flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-sm hover:bg-accent hover:text-accent-foreground"
                  onClick={() => { setTeamFilter(ALL_TEAMS); close() }}
                >
                  <Check className={cn('h-4 w-4 shrink-0', teamFilter === ALL_TEAMS ? 'opacity-100' : 'opacity-0')} />
                  All Teams
                </button>
                {teams.map(t => (
                  <button
                    key={t.teamId}
                    type="button"
                    className="flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-sm hover:bg-accent hover:text-accent-foreground"
                    onClick={() => { setTeamFilter(t.abbreviation); close() }}
                  >
                    <Check className={cn('h-4 w-4 shrink-0', teamFilter === t.abbreviation ? 'opacity-100' : 'opacity-0')} />
                    {t.name}
                  </button>
                ))}
              </>
            )}
          </SimpleDropdown>

          <SimpleDropdown
            align="end"
            panelClassName="w-48 max-h-72 overflow-y-auto"
            trigger={
              <Button variant="outline" size="sm" className="h-8 px-3 text-xs">
                <SlidersHorizontal className="mr-1.5 h-3.5 w-3.5" />
                Columns
              </Button>
            }
          >
            {() => (
              <>
                <div className="flex items-center justify-between px-2 py-1.5">
                  <p className="text-xs font-medium text-muted-foreground">{category} stats</p>
                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      onClick={selectAllColumns}
                      className="text-xs font-medium text-primary hover:underline"
                    >
                      All
                    </button>
                    <button
                      type="button"
                      onClick={deselectAllColumns}
                      className="text-xs font-medium text-primary hover:underline"
                    >
                      None
                    </button>
                  </div>
                </div>
                <div className="-mx-1 my-1 h-px bg-border" />
                {statColumns.length === 0 && (
                  <p className="px-2 py-1.5 text-xs text-muted-foreground italic">
                    No stats yet for this category.
                  </p>
                )}
                {rankedColumns.map(col => (
                  <button
                    key={col}
                    type="button"
                    onClick={() => toggleColumn(col)}
                    className="flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-sm hover:bg-accent hover:text-accent-foreground"
                  >
                    <Check className={cn('h-4 w-4 shrink-0', hiddenSet.has(col) ? 'opacity-0' : 'opacity-100')} />
                    {col}
                  </button>
                ))}
              </>
            )}
          </SimpleDropdown>
        </div>
      </div>

      {isLoading && (
        <div className="space-y-2">
          {[0, 1, 2].map(i => (
            <Skeleton key={i} className="h-8 w-full" />
          ))}
        </div>
      )}

      {isError && (
        <p className="text-sm text-muted-foreground italic">Could not load leaderboard.</p>
      )}

      {!isLoading && !isError && players.length === 0 && (
        <p className="text-sm text-muted-foreground italic">
          {teamFilter === ALL_TEAMS
            ? `No ${category.toLowerCase()} stats captured yet — this fills in as games are played.`
            : `No ${category.toLowerCase()} stats for ${selectedTeamLabel} yet.`}
        </p>
      )}

      {!isLoading && !isError && players.length > 0 && (
        <div className="rounded-md border overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow className="hover:bg-transparent">
                <TableHead className="h-10 text-xs font-medium">Player</TableHead>
                <TableHead className="h-10 text-xs font-medium">Team</TableHead>
                <TableHead className="h-10 text-xs font-medium">GP</TableHead>
                {visibleStatColumns.map(col => (
                  <TableHead key={col} className="h-10 text-xs font-medium">
                    <button
                      onClick={() => toggleSort(col)}
                      className="flex items-center gap-1 hover:text-foreground text-muted-foreground transition-colors whitespace-nowrap"
                    >
                      {col}
                      <ArrowUpDown className="h-3.5 w-3.5" />
                    </button>
                  </TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {sorted.map(p => (
                <TableRow key={p.playerId}>
                  <TableCell className="py-2.5 text-sm font-medium whitespace-nowrap">
                    <button
                      onClick={() => openPlayerCard(p.playerId)}
                      className="hover:underline"
                    >
                      {p.name}
                    </button>
                  </TableCell>
                  <TableCell className="py-2.5 text-sm text-muted-foreground">
                    <span className="flex items-center gap-1.5">
                      <TeamLogo sport="nfl" abbreviation={p.teamAbbreviation} className="h-4 w-4" />
                      {p.teamAbbreviation ?? '–'}
                    </span>
                  </TableCell>
                  <TableCell className="py-2.5 text-sm tabular-nums text-muted-foreground">
                    {p.gamesPlayed}
                  </TableCell>
                  {visibleStatColumns.map(col => (
                    <TableCell key={col} className="py-2.5 text-sm tabular-nums text-muted-foreground">
                      {p.stats[col] ?? '–'}
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </CollapsibleSection>
  )
}
