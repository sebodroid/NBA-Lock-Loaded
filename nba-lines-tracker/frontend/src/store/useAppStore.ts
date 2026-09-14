import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import { applyPalette, DEFAULT_PALETTE, type PaletteKey } from '@/lib/palettes'

interface AppStore {
  theme: 'light' | 'dark'
  toggleTheme: () => void
  palette: PaletteKey
  setPalette: (key: PaletteKey) => void
  isAuthenticated: boolean
  setAuthenticated: (value: boolean) => void
  openPanels: number[]
  openPanel: (teamId: number) => void
  closePanel: (teamId: number) => void
  selectedPlayerId: number | null
  openPlayerCard: (playerId: number) => void
  closePlayerCard: () => void
  gamePropsGame: { id: number; title: string } | null
  openGameProps: (game: { id: number; title: string }) => void
  closeGameProps: () => void
  betDraft: BetDraft | null
  openBetDraft: (draft: BetDraft) => void
  closeBetDraft: () => void
}

export interface BetDraft {
  gameId: number
  kind: 'PlayerProp' | 'Spread' | 'Total'
  playerPropLineId?: number
  side: 'Over' | 'Under' | 'Home' | 'Away'
  label: string       // e.g. "Patrick Mahomes — Passing Yards"
  oddsPreview: number // for the "to win" preview only — the server snapshots the real odds at submit time
}

export const useAppStore = create<AppStore>()(
  persist(
    (set) => ({
      theme: 'dark',   // default — matches the app's dark purple/gold design
      toggleTheme: () => set(state => {
        const next = state.theme === 'light' ? 'dark' : 'light'
        document.documentElement.classList.toggle('dark', next === 'dark')
        return { theme: next }
      }),
      palette: DEFAULT_PALETTE,
      setPalette: (key) => {
        applyPalette(key)
        set({ palette: key })
      },
      isAuthenticated: false,
      setAuthenticated: (value) => set({ isAuthenticated: value }),
      openPanels: [],
      openPanel: (teamId) => set(state => {
        if (state.openPanels.includes(teamId)) {
          document.getElementById(`panel-${teamId}`)?.scrollIntoView({ behavior: 'smooth', inline: 'nearest' })
          return state
        }
        return { openPanels: [...state.openPanels, teamId] }
      }),
      closePanel: (teamId) => set(state => ({
        openPanels: state.openPanels.filter(id => id !== teamId),
      })),
      selectedPlayerId: null,
      openPlayerCard: (playerId) => set({ selectedPlayerId: playerId }),
      closePlayerCard: () => set({ selectedPlayerId: null }),
      gamePropsGame: null,
      openGameProps: (game) => set({ gamePropsGame: game }),
      closeGameProps: () => set({ gamePropsGame: null }),
      betDraft: null,
      openBetDraft: (draft) => set({ betDraft: draft }),
      closeBetDraft: () => set({ betDraft: null }),
    }),
    {
      name: 'nba-app-store',
      // theme + palette persist; auth/panels/etc. reset on reload
      partialize: state => ({ theme: state.theme, palette: state.palette }),
      onRehydrateStorage: () => (state) => {
        // Apply persisted theme class + palette tokens on hydration
        if (state?.theme === 'dark') {
          document.documentElement.classList.add('dark')
        }
        applyPalette(state?.palette ?? DEFAULT_PALETTE)
      },
    }
  )
)
