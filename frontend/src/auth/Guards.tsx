import { Navigate, Outlet, useLocation } from 'react-router'
import { Box, CircularProgress } from '@mui/material'
import { useAuth } from './AuthContext'
import type { Role } from '../api/types'

export function FullPageSpinner() {
  return <Box sx={{ display: 'grid', placeItems: 'center', minHeight: '60vh' }}><CircularProgress size={28} /></Box>
}

export function RequireAuth() {
  const { state } = useAuth()
  const location = useLocation()
  if (state.status === 'loading') return <FullPageSpinner />
  if (state.status === 'anonymous') return <Navigate to="/login" replace state={{ from: location.pathname }} />
  return <Outlet />
}

export function RequireRole({ role }: { role: Role }) {
  const { user } = useAuth()
  return user?.role === role ? <Outlet /> : <Navigate to="/" replace />
}

export function AnonymousOnly() {
  const { state } = useAuth()
  if (state.status === 'loading') return <FullPageSpinner />
  return state.status === 'authenticated' ? <Navigate to="/" replace /> : <Outlet />
}
