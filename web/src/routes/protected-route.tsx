import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '@/features/auth/use-auth'
import { AuthLoading } from '@/features/auth/auth-loading'
import type { UserRole } from '@/lib/api/types'
import { routeAccessDecision } from './route-access'

interface ProtectedRouteProps {
  /** Roles allowed through. Omit to require only that the user is signed in. */
  allow?: UserRole[]
}

/**
 * Client-side guard. This is a usability measure, not a security boundary - the API
 * enforces the real check via its Student/Staff/Admin policies. A user who edits their
 * stored role gets a nicer-looking page and still receives 403s from every endpoint.
 */
export function ProtectedRoute({ allow }: ProtectedRouteProps) {
  const { user, isInitializing } = useAuth()
  const location = useLocation()

  const access = routeAccessDecision(user, isInitializing, allow)
  if (access === 'loading') return <AuthLoading />

  if (access === 'login') {
    // Remember the attempted URL so login can send them back to it.
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  if (access === 'forbidden') {
    return <Navigate to="/forbidden" replace />
  }

  return <Outlet />
}
