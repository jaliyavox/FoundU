import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { LogOutIcon, PackageSearchIcon, ShieldIcon, FileTextIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/features/auth/use-auth'
import { cn } from '@/lib/utils'
import type { UserRole } from '@/lib/api/types'

interface NavItem {
  to: string
  label: string
  icon: typeof PackageSearchIcon
  allow: UserRole[]
}

const NAV_ITEMS: NavItem[] = [
  { to: '/items', label: 'Found items', icon: PackageSearchIcon, allow: ['Staff', 'Admin'] },
  { to: '/my-reports', label: 'My reports', icon: FileTextIcon, allow: ['Student'] },
  { to: '/admin', label: 'Administration', icon: ShieldIcon, allow: ['Admin'] },
]

export function AppLayout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  async function handleLogout() {
    await logout()
    navigate('/login', { replace: true })
  }

  // The guard guarantees a user here, but narrowing keeps TypeScript honest.
  if (!user) return null

  const visibleItems = NAV_ITEMS.filter((item) => item.allow.includes(user.role))

  return (
    <div className="flex min-h-svh">
      <aside className="hidden w-60 shrink-0 flex-col border-r bg-sidebar text-sidebar-foreground sm:flex">
        <div className="px-5 py-5">
          <p className="text-lg font-semibold tracking-tight">FoundU</p>
          <p className="text-xs text-muted-foreground">Campus lost &amp; found</p>
        </div>

        <nav aria-label="Main" className="flex flex-1 flex-col gap-1 px-3">
          {visibleItems.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-2.5 rounded-md px-3 py-2 text-sm transition-colors',
                  'focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none',
                  isActive
                    ? 'bg-sidebar-accent font-medium text-sidebar-accent-foreground'
                    : 'text-muted-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground',
                )
              }
            >
              <Icon className="size-4" aria-hidden="true" />
              {label}
            </NavLink>
          ))}
        </nav>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center justify-between gap-4 border-b px-6 py-3">
          <div className="min-w-0">
            <p className="truncate text-sm font-medium">{user.fullName}</p>
            <p className="text-xs text-muted-foreground">{user.role}</p>
          </div>

          <Button variant="outline" size="sm" onClick={handleLogout}>
            <LogOutIcon aria-hidden="true" />
            Sign out
          </Button>
        </header>

        <main className="flex-1 px-6 py-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
