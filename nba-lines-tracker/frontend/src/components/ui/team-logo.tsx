import { useState } from 'react'
import { getTeamLogoUrl } from '@/lib/teamBranding'
import type { Sport } from '@/lib/sports'
import { cn } from '@/lib/utils'

interface TeamLogoProps {
  sport: Sport
  abbreviation: string | null | undefined
  className?: string
}

// Renders nothing (not a broken-image icon) if there's no abbreviation to work with,
// or if ESPN's CDN 404s on our best-effort URL guess.
export function TeamLogo({ sport, abbreviation, className }: TeamLogoProps) {
  const [failed, setFailed] = useState(false)
  const url = getTeamLogoUrl(sport, abbreviation)
  if (!url || failed) return null

  return (
    <img
      src={url}
      alt=""
      className={cn('object-contain shrink-0', className)}
      onError={() => setFailed(true)}
      loading="lazy"
    />
  )
}
