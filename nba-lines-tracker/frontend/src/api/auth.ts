import axios from 'axios'
import { apiBaseUrl, apiClient, setAccessToken } from './client'

export async function login(email: string, password: string): Promise<{ isAdmin: boolean }> {
  const { data } = await axios.post(`${apiBaseUrl}/api/auth/login`, { email, password })
  setAccessToken(data.accessToken)
  localStorage.setItem('refreshToken', data.refreshToken)
  return { isAdmin: data.isAdmin }
}

// Distinct from "wrong password" (login() just throws a generic error) — register()
// never logs the user in, so there's no isAdmin to return.
export async function register(email: string, username: string, password: string): Promise<void> {
  await axios.post(`${apiBaseUrl}/api/auth/register`, { email, username, password })
}

export async function logout(): Promise<void> {
  const refreshToken = localStorage.getItem('refreshToken')
  if (refreshToken) {
    try { await apiClient.post('/api/auth/logout', { refreshToken }) } catch { /* best effort */ }
  }
  setAccessToken(null)
  localStorage.removeItem('refreshToken')
}

export async function tryRestoreSession(): Promise<{ ok: boolean; isAdmin: boolean }> {
  const refreshToken = localStorage.getItem('refreshToken')
  if (!refreshToken) return { ok: false, isAdmin: false }
  try {
    const { data } = await axios.post(`${apiBaseUrl}/api/auth/refresh`, { refreshToken })
    setAccessToken(data.accessToken)
    localStorage.setItem('refreshToken', data.refreshToken)
    return { ok: true, isAdmin: data.isAdmin }
  } catch {
    localStorage.removeItem('refreshToken')
    return { ok: false, isAdmin: false }
  }
}
