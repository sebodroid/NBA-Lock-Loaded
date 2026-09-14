import { useEffect, useState } from 'react'
import { createBrowserRouter, RouterProvider, Navigate, Outlet, useParams } from 'react-router-dom'
import { LoginPage } from '@/components/auth/LoginPage'
import { MainPage } from '@/components/layout/MainPage'
import { useAppStore } from '@/store/useAppStore'
import { tryRestoreSession } from '@/api/auth'
import { isSport } from '@/lib/sports'

function ProtectedRoute() {
  const isAuthenticated = useAppStore(s => s.isAuthenticated)
  return isAuthenticated ? <Outlet /> : <Navigate to="/login" replace />
}

function PublicRoute() {
  const isAuthenticated = useAppStore(s => s.isAuthenticated)
  return isAuthenticated ? <Navigate to="/" replace /> : <Outlet />
}

// Guards against an unknown value in the :sport segment (e.g. /nhl) — falls
// back to /nfl rather than rendering a page that queries a bogus API route.
function SportRoute() {
  const { sport } = useParams<{ sport: string }>()
  return isSport(sport) ? <MainPage /> : <Navigate to="/nfl" replace />
}

const router = createBrowserRouter([
  {
    element: <ProtectedRoute />,
    children: [
      { path: '/', element: <Navigate to="/nfl" replace /> },
      { path: '/:sport', element: <SportRoute /> },
    ],
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
