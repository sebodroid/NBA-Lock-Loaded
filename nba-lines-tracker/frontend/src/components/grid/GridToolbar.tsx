import { Table } from '@tanstack/react-table'
import type { TeamStatsResponse } from '@/types/api'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { SlidersHorizontal, Check } from 'lucide-react'
import { cn } from '@/lib/utils'

const CONFERENCES = ['East', 'West'] as const

interface GridToolbarProps {
  table: Table<TeamStatsResponse>
  conferenceFilter: string | null
  setConferenceFilter: (v: string | null) => void
}

export function GridToolbar({
  table,
  conferenceFilter,
  setConferenceFilter,
}: GridToolbarProps) {
  return (
    <div className="flex items-center gap-2 py-3">
      {/* Conference filter — toggle buttons */}
      <div className="flex items-center gap-1">
        {CONFERENCES.map(conf => (
          <Button
            key={conf}
            variant={conferenceFilter === conf ? 'default' : 'outline'}
            size="sm"
            onClick={() => setConferenceFilter(conferenceFilter === conf ? null : conf)}
            className="h-8 px-3 text-xs"
          >
            {conf}
          </Button>
        ))}
      </div>

      <div className="flex-1" />

      {/* Column visibility toggle */}
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="outline" size="sm" className="h-8 text-xs">
            <SlidersHorizontal className="mr-1.5 h-3.5 w-3.5" />
            Columns
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-40">
          <DropdownMenuLabel>Toggle columns</DropdownMenuLabel>
          <DropdownMenuSeparator />
          {table
            .getAllColumns()
            .filter(col => col.getCanHide())
            .map(col => (
              <DropdownMenuItem
                key={col.id}
                className="capitalize"
                onSelect={e => {
                  e.preventDefault()
                  col.toggleVisibility(!col.getIsVisible())
                }}
              >
                <Check
                  className={cn('mr-2 h-4 w-4', col.getIsVisible() ? 'opacity-100' : 'opacity-0')}
                />
                {col.id === 'atsPct' ? 'ATS%'
                  : col.id === 'atsRecord' ? 'ATS Record'
                  : col.id === 'ouPct' ? 'O/U%'
                  : col.id === 'ouRecord' ? 'O/U Record'
                  : col.id}
              </DropdownMenuItem>
            ))}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  )
}
