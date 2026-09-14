import { NFL_TEAMS } from './teamBranding'

// Every team's palette (32 of them) plus the app's own default — a long list, but a
// bounded one: it's exactly "every NFL team," never more, and the picker scrolls rather
// than growing the page.
export interface Palette {
  key: string
  label: string
  primary: string
  primaryForeground: string
  gold: string
  goldForeground: string
}

// --- color helpers -----------------------------------------------------------------
// Real brand colors run from near-black (Raiders) to near-white (Saints gold) — neither
// works as-is for a UI accent (a button, an active tab) against the app's own dark
// background. usableAccent() nudges lightness/saturation into a range that always reads
// as a distinct color chip; pickForeground() then picks readable text for whatever comes
// out, so this works for all 32 teams without hand-tuning each one.

function hexToRgb(hex: string): [number, number, number] {
  const c = hex.replace('#', '')
  return [parseInt(c.slice(0, 2), 16), parseInt(c.slice(2, 4), 16), parseInt(c.slice(4, 6), 16)]
}

function rgbToHsl(r: number, g: number, b: number): [number, number, number] {
  r /= 255; g /= 255; b /= 255
  const max = Math.max(r, g, b), min = Math.min(r, g, b)
  let h = 0, s = 0
  const l = (max + min) / 2
  if (max !== min) {
    const d = max - min
    s = l > 0.5 ? d / (2 - max - min) : d / (max + min)
    switch (max) {
      case r: h = (g - b) / d + (g < b ? 6 : 0); break
      case g: h = (b - r) / d + 2; break
      default: h = (r - g) / d + 4
    }
    h /= 6
  }
  return [h * 360, s * 100, l * 100]
}

function hslToHex(h: number, s: number, l: number): string {
  s /= 100; l /= 100
  const k = (n: number) => (n + h / 30) % 12
  const a = s * Math.min(l, 1 - l)
  const f = (n: number) => l - a * Math.max(-1, Math.min(k(n) - 3, Math.min(9 - k(n), 1)))
  const toHex = (x: number) => Math.round(x * 255).toString(16).padStart(2, '0')
  return `#${toHex(f(0))}${toHex(f(8))}${toHex(f(4))}`
}

function usableAccent(hex: string): string {
  const [h, s, l] = rgbToHsl(...hexToRgb(hex))
  return hslToHex(h, Math.max(s, 50), Math.min(Math.max(l, 34), 62))
}

function pickForeground(hex: string): string {
  const [r, g, b] = hexToRgb(hex)
  const luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255
  return luminance > 0.55 ? '#17121F' : '#FAFAFA'
}

function paletteFromBrand(key: string, label: string, primary: string, secondary: string): Palette {
  const p = usableAccent(primary)
  const g = usableAccent(secondary)
  return { key, label, primary: p, primaryForeground: pickForeground(p), gold: g, goldForeground: pickForeground(g) }
}

// --- the list ------------------------------------------------------------------------

export const PALETTES: Palette[] = [
  { key: 'default', label: 'Violet (Default)', primary: '#8B5CF6', primaryForeground: '#FAFAFA', gold: '#D4AF37', goldForeground: '#1A1625' },
  ...NFL_TEAMS.map(t => paletteFromBrand(t.abbreviation.toLowerCase(), t.name, t.primary, t.secondary)),
]

export type PaletteKey = string
export const DEFAULT_PALETTE: PaletteKey = 'default'

export function getPalette(key: string): Palette {
  return PALETTES.find(p => p.key === key) ?? PALETTES[0]
}

// Overrides the accent tokens on the root element via inline style, which beats the
// stylesheet's :root/.dark declarations regardless of which theme is active.
export function applyPalette(key: string) {
  const p = getPalette(key)
  const root = document.documentElement.style
  root.setProperty('--primary', p.primary)
  root.setProperty('--primary-foreground', p.primaryForeground)
  root.setProperty('--ring', p.primary)
  root.setProperty('--gold', p.gold)
  root.setProperty('--gold-foreground', p.goldForeground)
}
