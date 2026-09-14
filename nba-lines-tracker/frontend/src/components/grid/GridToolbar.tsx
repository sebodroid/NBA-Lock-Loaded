import { Table } from '@tanstack/react-table'
import type { TeamStatsResponse } from '@/types/api'
import { Button } from '@/components/ui/button'
import { SimpleDropdown } from '@/components/ui/simple-dropdown'
import { SlidersHorizontal, Check } from 'lucide-react'
import { cn } from '@/lib/utils'

const COLUMN_LABELS: Record<string, string> = {
  atsPct: 'ATS%',
  atsRecord: 'ATS Record',
  ouPct: 'O/U%',
  ouRecord: 'O/U Record',
}

interface GridToolbarProps {
  table: Table<TeamStatsResponse>
  conferenceOptions: readonly string[]
  conferenceFilter: string | null
  setConferenceFilter: (v: string | null) => void
}

export function GridToolbar({
  table,
  conferenceOptions,
  conferenceFilter,
  setConferenceFilter,
}: GridToolbarProps) {
  const hideable = table.getAllColumns().filter(col => col.getCanHide())

  return (
    <div className="flex items-center gap-2 py-3">
      {/* Conference filter — toggle buttons; hidden entirely when the sport has no conference data */}
      {conferenceOptions.length > 0 && (
      <div className="flex items-center gap-1">
        {conferenceOptions.map(conf => (
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
      )}

      <div className="flex-1" />

      {/* Column visibility */}
      <SimpleDropdown
        align="end"
        panelClassName="w-40"
        trigger={
          <Button variant="outline" size="sm" className="h-8 text-xs">
            <SlidersHorizontal className="mr-1.5 h-3.5 w-3.5" />
            Columns
          </Button>
        }
      >
        {() => (
          <>
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
          </>
        )}
      </SimpleDropdown>
    </div>
  )
}
