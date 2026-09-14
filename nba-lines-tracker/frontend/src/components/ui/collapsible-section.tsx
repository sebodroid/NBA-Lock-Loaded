import { useState, type ReactNode } from 'react'
import { ChevronDown, ChevronRight } from 'lucide-react'

interface CollapsibleSectionProps {
  title: ReactNode
  defaultOpen?: boolean
  children: ReactNode
}

// Plain state-based collapse — no library, matching the SimpleDropdown decision.
export function CollapsibleSection({ title, defaultOpen = true, children }: CollapsibleSectionProps) {
  const [open, setOpen] = useState(defaultOpen)

  return (
    <div className="mt-6 border-t pt-4">
      <button
        onClick={() => setOpen(o => !o)}
        className="flex items-center gap-1.5 mb-3 text-sm font-medium text-muted-foreground hover:text-foreground"
      >
        {open ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
        {title}
      </button>

      {open && children}
    </div>
  )
}
