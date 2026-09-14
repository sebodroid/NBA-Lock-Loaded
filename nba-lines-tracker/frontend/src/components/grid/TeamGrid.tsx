import { useMemo, useState } from 'react'
import { useParams } from 'react-router-dom'
import {
  useReactTable,
  getCoreRowModel,
  getSortedRowModel,
  flexRender,
  SortingState,
  VisibilityState,
} from '@tanstack/react-table'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Skeleton } from '@/components/ui/skeleton'
import { Button } from '@/components/ui/button'
import { CollapsibleSection } from '@/components/ui/collapsible-section'
import { useTeams } from '@/api/teams'
import { useAppStore } from '@/store/useAppStore'
import { useLocalStorage } from '@/hooks/useLocalStorage'
import { teamColumns } from './columns'
import { GridToolbar } from './GridToolbar'
import type { TeamStatsResponse } from '@/types/api'
import { CONFERENCE_OPTIONS, type Sport } from '@/lib/sports'
import { getTeamBrand } from '@/lib/teamBranding'

const DEFAULT_VISIBILITY: VisibilityState = {
  team: true,
  record: true,
  streak: true,
  atsPct: true,
  atsRecord: true,
  ouPct: true,
  ouRecord: true,
  conference: false,
  division: false,
}

const DEFAULT_SORT: SortingState = [{ id: 'atsPct', desc: true }]

// Only NFL has a historical season backfilled right now — extend this when other
// sports get one too.
const HISTORICAL_SEASONS: Record<Sport, string[]> = {
  nfl: ['2025'],
  nba: [],
  mlb: [],
}

export function TeamGrid() {
  const { sport } = useParams<{ sport: Sport }>()
  const conferenceOptions = sport ? CONFERENCE_OPTIONS[sport] : []
  const historicalSeasons = sport ? HISTORICAL_SEASONS[sport] : []
  const [season, setSeason] = useState<string | undefined>(undefined)   // undefined = live current season
  const { data: teams = [], isLoading, isError } = useTeams(season)
  const openPanel = useAppStore(s => s.openPanel)

  const [sorting, setSorting] = useState<SortingState>(DEFAULT_SORT)
  const [columnVisibility, setColumnVisibility] = useLocalStorage<VisibilityState>(
    'nba-grid-column-visibility',
    DEFAULT_VISIBILITY
  )
  const [conferenceFilter, setConferenceFilter] = useState<string | null>(null)

  const filteredTeams = useMemo<TeamStatsResponse[]>(() => {
    return teams.filter(t => {
      if (conferenceFilter && t.conference !== conferenceFilter) return false
      return true
    })
  }, [teams, conferenceFilter])

  const table = useReactTable({
    data: filteredTeams,
    columns: teamColumns,
    state: { sorting, columnVisibility },
    onSortingChange: setSorting,
    onColumnVisibilityChange: setColumnVisibility,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
  })

  if (isError) {
    return (
      <div className="rounded-md border border-destructive/50 bg-destructive/10 p-4 text-sm text-destructive">
        Failed to load team data. Please refresh the page.
      </div>
    )
  }

  return (
    <CollapsibleSection title="Teams">
    <div className="space-y-1">
      {historicalSeasons.length > 0 && (
        <div className="flex items-center gap-1">
          <Button
            variant={season === undefined ? 'default' : 'outline'}
            size="sm"
            onClick={() => setSeason(undefined)}
            className="h-7 px-2.5 text-xs"
          >
            Current
          </Button>
          {historicalSeasons.map(s => (
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
      )}
      <GridToolbar
        table={table}
        conferenceOptions={conferenceOptions}
        conferenceFilter={conferenceFilter}
        setConferenceFilter={setConferenceFilter}
      />
      <div className="rounded-md border">
        <Table>
          <TableHeader>
            {table.getHeaderGroups().map(hg => (
              <TableRow key={hg.id} className="hover:bg-transparent">
                {hg.headers.map(header => (
                  <TableHead key={header.id} className="h-10 text-xs font-medium">
                    {header.isPlaceholder
                      ? null
                      : flexRender(header.column.columnDef.header, header.getContext())}
                  </TableHead>
                ))}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody>
            {isLoading ? (
              Array.from({ length: 10 }).map((_, i) => (
                <TableRow key={i}>
                  {teamColumns.map((_, ci) => (
                    <TableCell key={ci}>
                      <Skeleton className="h-4 w-16" />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : table.getRowModel().rows.length === 0 ? (
              <TableRow>
                <TableCell colSpan={teamColumns.length} className="h-24 text-center text-muted-foreground text-sm">
                  No teams match the current filters.
                </TableCell>
              </TableRow>
            ) : (
              table.getRowModel().rows.map(row => {
                const brand = getTeamBrand(sport ?? 'nfl', row.original.abbreviation)
                return (
                  <TableRow
                    key={row.id}
                    onClick={() => openPanel(row.original.teamId)}
                    className="cursor-pointer border-l-4"
                    style={{ borderLeftColor: brand?.primary ?? 'transparent' }}
                    data-state={row.getIsSelected() ? 'selected' : undefined}
                  >
                    {row.getVisibleCells().map(cell => (
                      <TableCell key={cell.id} className="py-2.5 text-sm">
                        {flexRender(cell.column.columnDef.cell, cell.getContext())}
                      </TableCell>
                    ))}
                  </TableRow>
                )
              })
            )}
          </TableBody>
        </Table>
      </div>
    </div>
    </CollapsibleSection>
  )
}
