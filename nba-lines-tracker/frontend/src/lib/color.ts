import type { CSSProperties } from 'react'

/**
 * Returns inline style with background-color for a percentage value.
 * Above 50% = green, below 50% = red. Intensity scales with distance from 50%.
 * Push results are excluded from the percentage before calling this function.
 *
 * Same "soft badge" language in both themes — a translucent tint behind colored text
 * (the ATS/O-U result badges elsewhere in the app use this same look) — rather than the
 * original solid neon fill, which read as harsh in both light and dark, just more so
 * against the app's dark background. Only the tuning differs between the two.
 */
export function getPercentageColor(pct: number, isDark: boolean): CSSProperties {
  const distance = Math.abs(pct - 50)    // 0–50
  if (distance < 2) return {}             // within 2% of 50 — no color (too close to call)

  const hue = pct >= 50 ? 142 : 0        // 142 = green, 0 = red

  if (isDark) {
    const alpha = Math.min(0.14 + distance * 0.006, 0.32)
    const textLightness = Math.min(58 + distance * 0.4, 78)
    return {
      backgroundColor: `hsla(${hue}, 70%, 45%, ${alpha})`,
      color: `hsl(${hue}, 65%, ${textLightness}%)`,
    }
  }

  const alpha = Math.min(0.1 + distance * 0.005, 0.28)
  const textLightness = Math.max(38 - distance * 0.25, 22)
  return {
    backgroundColor: `hsla(${hue}, 65%, 45%, ${alpha})`,
    color: `hsl(${hue}, 70%, ${textLightness}%)`,
  }
}
