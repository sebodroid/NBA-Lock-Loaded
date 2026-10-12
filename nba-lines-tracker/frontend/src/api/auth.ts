import axios from 'axios'
import { apiBaseUrl, apiClient, setAccessToken } from './client'

export async function login(email: string, password: string): Promise<void> {
  const { data } = await axios.post(`${apiBaseUrl}/api/auth/login`, { email, password })
  setAccessToken(data.accessToken)
  localStorage.setItem('refreshToken', data.refreshToken)
}

export async function logout(): Promise<void> {
  const refreshToken = localStorage.getItem('refreshToken')
  if (refreshToken) {
    try { await apiClient.post('/api/auth/logout', { refreshToken }) } catch { /* best effort */ }
  }
  setAccessToken(null)
  localStorage.removeItem('refreshToken')
}

export async function tryRestoreSession(): Promise<boolean> {
  const refreshToken = localStorage.getItem('refreshToken')
  if (!refreshToken) return false
  try {
    const { data } = await axios.post(`${apiBaseUrl}/api/auth/refresh`, { refreshToken })
    setAccessToken(data.accessToken)
    localStorage.setItem('refreshToken', data.refreshToken)
    return true
  } catch {
    localStorage.removeItem('refreshToken')
    return false
  }
}
