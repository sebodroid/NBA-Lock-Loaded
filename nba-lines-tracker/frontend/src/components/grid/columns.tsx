import { ColumnDef } from '@tanstack/react-table'
import { ArrowUpDown } from 'lucide-react'
import type { TeamStatsResponse } from '@/types/api'
import { calcAtsPct, calcOuPct, formatStreak } from '@/types/api'
import { AtsCell } from './AtsCell'

function SortHeader({ label, column }: { label: string; column: { toggleSorting: () => void } }) {
  return (
    <button
      onClick={() => column.toggleSorting()}
      className="flex items-center gap-1 hover:text-foreground text-muted-foreground transition-colors"
    >
      {label}
      <ArrowUpDown className="h-3.5 w-3.5" />
    </button>
  )
}

export const teamColumns: ColumnDef<TeamStatsResponse>[] = [
  {
    id: 'team',
    accessorKey: 'name',
    header: ({ column }) => <SortHeader label="Team" column={column} />,
    cell: ({ row }) => (
      <span className="font-medium">{row.original.name}</span>
    ),
    enableHiding: false,   // team name always visible — no toggle
  },
  {
    id: 'record',
    header: ({ column }) => <SortHeader label="W-L" column={column} />,
    accessorFn: row => row.wins,
    cell: ({ row }) => (
      <span className="tabular-nums text-muted-foreground">
        {row.original.wins}–{row.original.losses}
      </span>
    ),
  },
  {
    id: 'streak',
    header: ({ column }) => <SortHeader label="Streak" column={column} />,
    accessorFn: row => row.streak,
    cell: ({ row }) => {
      const s = row.original.streak
      const text = formatStreak(s)
      const color = s > 0
        ? 'text-green-600 dark:text-green-400'
        : s < 0
        ? 'text-red-500 dark:text-red-400'
        : 'text-muted-foreground'
      return <span className={`font-medium tabular-nums ${color}`}>{text}</span>
    },
  },
  {
    id: 'atsPct',
    header: ({ column }) => <SortHeader label="ATS%" column={column} />,
    accessorFn: row => calcAtsPct(row) ?? -1,   // -1 so null sorts last
    cell: ({ row }) => <AtsCell pct={calcAtsPct(row.original)} />,
    sortingFn: (a, b) => {
      const av = calcAtsPct(a.original) ?? -1
      const bv = calcAtsPct(b.original) ?? -1
      return av - bv
    },
  },
  {
    id: 'atsRecord',
    header: 'ATS Record',
    accessorFn: row => row.atsCovers,
    cell: ({ row }) => (
      <span className="tabular-nums text-muted-foreground text-sm">
        {row.original.atsCovers}–{row.original.atsLosses}–{row.original.atsPushes}
      </span>
    ),
  },
  {
    id: 'ouPct',
    header: ({ column }) => <SortHeader label="O/U%" column={column} />,
    accessorFn: row => calcOuPct(row) ?? -1,
    cell: ({ row }) => <AtsCell pct={calcOuPct(row.original)} />,
    sortingFn: (a, b) => {
      const av = calcOuPct(a.original) ?? -1
      const bv = calcOuPct(b.original) ?? -1
      return av - bv
    },
  },
  {
    id: 'ouRecord',
    header: 'O/U Record',
    accessorFn: row => row.ouOvers,
    cell: ({ row }) => (
      <span className="tabular-nums text-muted-foreground text-sm">
        {row.original.ouOvers}–{row.original.ouUnders}–{row.original.ouPushes}
      </span>
    ),
  },
  // Hidden by default — available via column visibility toggle
  {
    id: 'conference',
    accessorKey: 'conference',
    header: 'Conference',
    cell: ({ getValue }) => getValue() ?? '–',
    enableSorting: false,
  },
  {
    id: 'division',
    accessorKey: 'division',
    header: 'Division',
    cell: ({ getValue }) => getValue() ?? '–',
    enableSorting: false,
  },
]
