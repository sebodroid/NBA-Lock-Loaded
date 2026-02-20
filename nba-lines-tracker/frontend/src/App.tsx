import { useEffect, useState } from 'react'
import { createBrowserRouter, RouterProvider, Navigate, Outlet } from 'react-router-dom'
import { LoginPage } from '@/components/auth/LoginPage'
import { useAppStore } from '@/store/useAppStore'
import { tryRestoreSession } from '@/api/auth'

// Placeholder — replaced in Plan 04-02
function MainPage() {
  return <div className="p-8"><h1 className="text-xl font-semibold">Loading team data...</h1></div>
}

function ProtectedRoute() {
  const isAuthenticated = useAppStore(s => s.isAuthenticated)
  return isAuthenticated ? <Outlet /> : <Navigate to="/login" replace />
}

function PublicRoute() {
  const isAuthenticated = useAppStore(s => s.isAuthenticated)
  return isAuthenticated ? <Navigate to="/" replace /> : <Outlet />
}

const router = createBrowserRouter([
  {
    element: <ProtectedRoute />,
    children: [{ path: '/', element: <MainPage /> }],
  },
  {
    element: <PublicRoute />,
    children: [{ path: '/login', element: <LoginPage /> }],
  },
])

export default function App() {
  const setAuthenticated = useAppStore(s => s.setAuthenticated)
  const [restoring, setRestoring] = useState(true)

  useEffect(() => {
    tryRestoreSession().then(ok => {
      setAuthenticated(ok)
      setRestoring(false)
    })
  }, [setAuthenticated])

  if (restoring) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background">
        <p className="text-muted-foreground text-sm">Loading...</p>
      </div>
    )
  }

  return <RouterProvider router={router} />
}
