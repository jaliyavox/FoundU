import type { UserRole } from '@/lib/api/types'

export type RouteAccess = 'loading' | 'login' | 'forbidden' | 'allowed'

/** Pure client-side guard policy; ASP.NET remains the authorization boundary. */
export function routeAccessDecision(
  user: { role: UserRole } | null,
  isInitializing: boolean,
  allow?: UserRole[],
): RouteAccess {
  if (isInitializing) return 'loading'
  if (!user) return 'login'
  return allow && !allow.includes(user.role) ? 'forbidden' : 'allowed'
}
