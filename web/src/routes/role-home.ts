import type { UserRole } from '@/lib/api/types'

/** Where each role lands after logging in, and where "home" points in the layout. */
export function homeRouteForRole(role: UserRole): string {
  switch (role) {
    case 'Admin':
      return '/admin'
    case 'Staff':
      return '/items'
    case 'Student':
      return '/my-reports'
  }
}

// Mirrors the role-restricted groups in router.tsx. Order matters: the more specific
// admin paths staff may use are checked before the admin-only `/admin` prefix.
const STAFF_ROUTES = ['/items', '/handovers', '/admin/support', '/admin/overview']
const STUDENT_ROUTES = ['/my-reports', '/my-claims', '/ask-foundu', '/help-to-find']

const under = (path: string, prefix: string) => path === prefix || path.startsWith(`${prefix}/`)

/**
 * Whether a role may open a path, so the login page does not send someone back to a page
 * that will only show them Forbidden. The API is still the authorization boundary.
 */
export function canRoleOpen(role: UserRole, pathname: string): boolean {
  if (STAFF_ROUTES.some(prefix => under(pathname, prefix)) || pathname === '/claims') {
    return role === 'Staff' || role === 'Admin'
  }
  if (STUDENT_ROUTES.some(prefix => under(pathname, prefix))) return role === 'Student'
  if (under(pathname, '/admin')) return role === 'Admin'
  return true
}
