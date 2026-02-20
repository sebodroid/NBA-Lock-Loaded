import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface AppStore {
  theme: 'light' | 'dark'
  toggleTheme: () => void
  isAuthenticated: boolean
  setAuthenticated: (value: boolean) => void
  openPanels: number[]
  openPanel: (teamId: number) => void
  closePanel: (teamId: number) => void
}

export const useAppStore = create<AppStore>()(
  persist(
    (set) => ({
      theme: 'light',
      toggleTheme: () => set(state => {
        const next = state.theme === 'light' ? 'dark' : 'light'
        document.documentElement.classList.toggle('dark', next === 'dark')
        return { theme: next }
      }),
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
    }),
    {
      name: 'nba-app-store',
      partialize: state => ({ theme: state.theme }),  // only persist theme; auth/panels reset on reload
      onRehydrateStorage: () => (state) => {
        // Apply persisted theme class on hydration
        if (state?.theme === 'dark') {
          document.documentElement.classList.add('dark')
        }
      },
    }
  )
)
