import { useState } from 'react'
import { getTeamLogoUrl } from '@/lib/teamBranding'
import type { Sport } from '@/lib/sports'
import { cn } from '@/lib/utils'

interface TeamLogoProps {
  sport: Sport
  abbreviation: string | null | undefined
  // A logo URL supplied by the API (CFB) — takes precedence over the abbreviation-based guess.
  src?: string | null
  className?: string
}

// Renders nothing (not a broken-image icon) if there's no logo to work with, or if the
// image 404s on our best-effort URL guess.
export function TeamLogo({ sport, abbreviation, src, className }: TeamLogoProps) {
  const [failed, setFailed] = useState(false)
  const url = src ?? getTeamLogoUrl(sport, abbreviation)
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
