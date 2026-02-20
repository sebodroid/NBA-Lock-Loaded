import axios from 'axios'

// Access token lives in module memory — NOT localStorage (XSS mitigation)
// Refresh token lives in localStorage (survives page reload)
let accessToken: string | null = null

export const apiClient = axios.create({
  baseURL: '',   // Vite proxy handles /api/* routing in dev; nginx in production
})

export function setAccessToken(token: string | null) {
  accessToken = token
}

export function getAccessToken(): string | null {
  return accessToken
}

// Attach access token to every outgoing request
apiClient.interceptors.request.use(config => {
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`
  }
  return config
})

// On 401: refresh once, queue concurrent requests, retry; on refresh failure: clear and redirect to login
let isRefreshing = false
let failedQueue: Array<{ resolve: (v: unknown) => void; reject: (e: unknown) => void }> = []

apiClient.interceptors.response.use(
  response => response,
  async error => {
    const original = error.config
    if (error.response?.status === 401 && !original._retry) {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject })
        }).then(() => apiClient(original))
      }

      original._retry = true
      isRefreshing = true

      try {
        const refreshToken = localStorage.getItem('refreshToken')
        if (!refreshToken) throw new Error('No refresh token')

        const { data } = await axios.post('/api/auth/refresh', { refreshToken })
        setAccessToken(data.accessToken)
        localStorage.setItem('refreshToken', data.refreshToken)

        failedQueue.forEach(p => p.resolve(undefined))
        failedQueue = []

        return apiClient(original)
      } catch (refreshError) {
        failedQueue.forEach(p => p.reject(refreshError))
        failedQueue = []
        setAccessToken(null)
        localStorage.removeItem('refreshToken')
        window.location.href = '/login'
        return Promise.reject(refreshError)
      } finally {
        isRefreshing = false
      }
    }
    return Promise.reject(error)
  }
)
