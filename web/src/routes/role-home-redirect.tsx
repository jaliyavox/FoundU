import { Navigate } from 'react-router-dom'
import { useAuth } from '@/features/auth/use-auth'
import { homeRouteForRole } from './role-home'

/** Sends "/" to whichever landing page suits the signed-in user's role. */
export function RoleHomeRedirect() {
  const { user } = useAuth()

  if (!user) return <Navigate to="/login" replace />

  return <Navigate to={homeRouteForRole(user.role)} replace />
}
