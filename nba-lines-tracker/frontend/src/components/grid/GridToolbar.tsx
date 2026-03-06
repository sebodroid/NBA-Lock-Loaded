import { Popover } from 'radix-ui'
import { Table } from '@tanstack/react-table'
import type { TeamStatsResponse } from '@/types/api'
import { Button } from '@/components/ui/button'
import { SlidersHorizontal, Check } from 'lucide-react'
import { cn } from '@/lib/utils'

const CONFERENCES = ['East', 'West'] as const

const COLUMN_LABELS: Record<string, string> = {
  atsPct: 'ATS%',
  atsRecord: 'ATS Record',
  ouPct: 'O/U%',
  ouRecord: 'O/U Record',
}

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
  const hideable = table.getAllColumns().filter(col => col.getCanHide())

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

      {/* Column visibility — Popover stays open while toggling */}
      <Popover.Root>
        <Popover.Trigger asChild>
          <Button variant="outline" size="sm" className="h-8 text-xs">
            <SlidersHorizontal className="mr-1.5 h-3.5 w-3.5" />
            Columns
          </Button>
        </Popover.Trigger>
        <Popover.Portal>
          <Popover.Content
            align="end"
            sideOffset={4}
            className="z-50 w-40 rounded-md border bg-popover p-1 shadow-md text-popover-foreground"
          >
            <p className="px-2 py-1.5 text-xs font-medium text-muted-foreground">Toggle columns</p>
            <div className="-mx-1 my-1 h-px bg-border" />
            {hideable.map(col => (
              <button
                key={col.id}
                type="button"
                className="flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-sm capitalize hover:bg-accent hover:text-accent-foreground"
                onClick={() => col.toggleVisibility(!col.getIsVisible())}
              >
                <Check
                  className={cn('h-4 w-4 shrink-0', col.getIsVisible() ? 'opacity-100' : 'opacity-0')}
                />
                {COLUMN_LABELS[col.id] ?? col.id}
              </button>
            ))}
          </Popover.Content>
        </Popover.Portal>
      </Popover.Root>
    </div>
  )
}
