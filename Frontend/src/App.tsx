import { useEffect, useState } from 'react'
import {
  BrowserRouter,
  Navigate,
  Route,
  Routes,
  useLocation,
  useNavigate,
} from 'react-router-dom'
import { bootstrapSessionFromUrl } from './auth/session'
import { AppLayout } from './layout/AppLayout'
import { DisclaimerPage } from './pages/DisclaimerPage'
import { Monatsansicht } from './pages/Monatsansicht'
import './App.css'

function AppRoutes() {
  const [session] = useState(() => bootstrapSessionFromUrl())
  const [mandantId, setMandantId] = useState<number | null>(
    () => session?.mandantId ?? null,
  )
  const location = useLocation()
  const navigate = useNavigate()

  useEffect(() => {
    const params = new URLSearchParams(location.search)
    if (mandantId != null) {
      params.set('mandantId', String(mandantId))
    } else {
      params.delete('mandantId')
    }
    const next = params.toString()
    const current = location.search.startsWith('?')
      ? location.search.slice(1)
      : location.search
    if (next !== current) {
      navigate(
        { pathname: location.pathname, search: next ? `?${next}` : '' },
        { replace: true },
      )
    }
  }, [mandantId, location.pathname, location.search, navigate])

  return (
    <Routes>
      <Route
        element={
          <AppLayout
            session={session}
            mandantId={mandantId}
            onMandantChange={setMandantId}
          />
        }
      >
        <Route
          index
          element={<Monatsansicht session={session} mandantId={mandantId} />}
        />
        <Route path="disclaimer" element={<DisclaimerPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AppRoutes />
    </BrowserRouter>
  )
}
