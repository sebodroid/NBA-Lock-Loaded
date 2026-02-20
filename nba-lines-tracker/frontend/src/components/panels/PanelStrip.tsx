import { ScrollArea, ScrollBar } from '@/components/ui/scroll-area'
import { useAppStore } from '@/store/useAppStore'
import { TeamPanel } from './TeamPanel'

export function PanelStrip() {
  const openPanels = useAppStore(s => s.openPanels)

  if (openPanels.length === 0) return null

  return (
    <div className="mt-6 border-t pt-4">
      <div className="flex items-center justify-between mb-3">
        <p className="text-sm font-medium text-muted-foreground">
          Team Comparison
          <span className="ml-2 text-xs text-muted-foreground/60">
            ({openPanels.length} {openPanels.length === 1 ? 'team' : 'teams'})
          </span>
        </p>
      </div>
      <ScrollArea className="w-full">
        <div className="flex gap-4 pb-4">
          {openPanels.map(teamId => (
            <TeamPanel key={teamId} teamId={teamId} />
          ))}
        </div>
        <ScrollBar orientation="horizontal" />
      </ScrollArea>
    </div>
  )
}
