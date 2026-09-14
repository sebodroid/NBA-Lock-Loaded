import { Palette as PaletteIcon, Check } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { SimpleDropdown } from '@/components/ui/simple-dropdown'
import { PALETTES } from '@/lib/palettes'
import { useAppStore } from '@/store/useAppStore'
import { cn } from '@/lib/utils'

export function PaletteMenu() {
  const palette = useAppStore(s => s.palette)
  const setPalette = useAppStore(s => s.setPalette)

  return (
    <SimpleDropdown
      align="end"
      panelClassName="w-52 max-h-80 overflow-y-auto"
      trigger={
        <Button variant="ghost" size="icon" className="h-8 w-8" aria-label="Change color palette">
          <PaletteIcon className="h-4 w-4" />
        </Button>
      }
    >
      {close => (
        <>
          <p className="px-2 py-1.5 text-xs font-medium text-muted-foreground">Color palette</p>
          <div className="-mx-1 my-1 h-px bg-border" />
          {PALETTES.map(p => (
            <button
              key={p.key}
              type="button"
              onClick={() => { setPalette(p.key); close() }}
              className="flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-sm hover:bg-accent hover:text-accent-foreground"
            >
              <Check className={cn('h-4 w-4 shrink-0', palette === p.key ? 'opacity-100' : 'opacity-0')} />
              <span
                className="h-3.5 w-3.5 rounded-full border border-white/20 shrink-0"
                style={{ background: `linear-gradient(135deg, ${p.primary} 50%, ${p.gold} 50%)` }}
              />
              {p.label}
            </button>
          ))}
        </>
      )}
    </SimpleDropdown>
  )
}
