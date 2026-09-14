import { useEffect, useRef, useState, type ReactNode } from 'react'
import { cn } from '@/lib/utils'

interface SimpleDropdownProps {
  trigger: ReactNode
  children: (close: () => void) => ReactNode
  align?: 'start' | 'end'
  panelClassName?: string
}

// Plain, dependency-free dropdown — no Portal, no Popper positioning, nothing that
// depends on a third-party library's rendering pipeline. Built after Radix's Popover
// and DropdownMenu both silently failed to render their content in this project
// (correct internal state, correct DOM structure per aria attributes, nothing visible
// on screen, no console error) — this trades a little polish for something that can't
// fail the same way, since it's just a div toggled by React state.
export function SimpleDropdown({ trigger, children, align = 'start', panelClassName }: SimpleDropdownProps) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return

    function handlePointerDown(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') setOpen(false)
    }

    document.addEventListener('mousedown', handlePointerDown)
    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('mousedown', handlePointerDown)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [open])

  return (
    <div ref={ref} className="relative inline-block">
      <div onClick={() => setOpen(o => !o)}>{trigger}</div>

      {open && (
        <div
          className={cn(
            'absolute z-50 mt-1 min-w-[10rem] rounded-md border bg-popover p-1 text-popover-foreground shadow-md',
            align === 'end' ? 'right-0' : 'left-0',
            panelClassName
          )}
        >
          {children(() => setOpen(false))}
        </div>
      )}
    </div>
  )
}
