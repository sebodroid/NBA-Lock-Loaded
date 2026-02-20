import { Table } from '@tanstack/react-table'
import type { TeamStatsResponse } from '@/types/api'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { SlidersHorizontal, ChevronDown } from 'lucide-react'

const CONFERENCES = ['East', 'West'] as const
const DIVISIONS = ['Atlantic', 'Central', 'Southeast', 'Northwest', 'Pacific', 'Southwest'] as const

interface GridToolbarProps {
  table: Table<TeamStatsResponse>
  conferenceFilter: string | null
  setConferenceFilter: (v: string | null) => void
  divisionFilter: string | null
  setDivisionFilter: (v: string | null) => void
}

export function GridToolbar({
  table,
  conferenceFilter,
  setConferenceFilter,
  divisionFilter,
  setDivisionFilter,
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

      {/* Division filter — dropdown select */}
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="outline" size="sm" className="h-8 text-xs">
            {divisionFilter ?? 'Division'}
            <ChevronDown className="ml-1 h-3.5 w-3.5" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="start">
          <DropdownMenuLabel>Filter by Division</DropdownMenuLabel>
          <DropdownMenuSeparator />
          {divisionFilter && (
            <>
              <DropdownMenuCheckboxItem
                checked={false}
                onCheckedChange={() => setDivisionFilter(null)}
              >
                Clear filter
              </DropdownMenuCheckboxItem>
              <DropdownMenuSeparator />
            </>
          )}
          {DIVISIONS.map(div => (
            <DropdownMenuCheckboxItem
              key={div}
              checked={divisionFilter === div}
              onCheckedChange={() => setDivisionFilter(divisionFilter === div ? null : div)}
            >
              {div}
            </DropdownMenuCheckboxItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>

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
              <DropdownMenuCheckboxItem
                key={col.id}
                checked={col.getIsVisible()}
                onCheckedChange={value => col.toggleVisibility(!!value)}
                className="capitalize"
              >
                {col.id === 'atsPct' ? 'ATS%'
                  : col.id === 'atsRecord' ? 'ATS Record'
                  : col.id === 'ouPct' ? 'O/U%'
                  : col.id === 'ouRecord' ? 'O/U Record'
                  : col.id}
              </DropdownMenuCheckboxItem>
            ))}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  )
}
