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
