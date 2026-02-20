import type { CSSProperties } from 'react'

/**
 * Returns inline style with background-color for a percentage value.
 * Above 50% = green, below 50% = red. Intensity scales with distance from 50%.
 * Push results are excluded from the percentage before calling this function.
 * Uses CSS custom properties so dark mode CSS variables override the hue/saturation.
 */
export function getPercentageColor(pct: number): CSSProperties {
  const distance = Math.abs(pct - 50)    // 0–50
  if (distance < 2) return {}             // within 2% of 50 — no color (too close to call)

  const saturation = Math.min(distance * 3, 80)   // 6%–80%
  const lightness = 95 - distance * 0.8           // 95%–55% — lighter near 50, darker near extremes
  const hue = pct >= 50 ? 142 : 0                 // 142 = green, 0 = red

  return {
    backgroundColor: `hsl(${hue}, ${saturation}%, ${lightness}%)`,
    color: lightness < 70 ? 'hsl(0, 0%, 98%)' : 'inherit',  // white text on saturated backgrounds
  }
}
