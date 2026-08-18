import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import {
  ChevronsUpDownIcon,
  FileTextIcon,
  LogOutIcon,
  PackageSearchIcon,
  ShieldIcon,
} from 'lucide-react'
import { FoundUMark } from '@/components/brand/foundu-logo'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Separator } from '@/components/ui/separator'
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarInset,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
  SidebarRail,
  SidebarTrigger,
} from '@/components/ui/sidebar'
import { useAuth } from '@/features/auth/use-auth'
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

/** First letters of the first and last name, e.g. "FoundU Dev Administrator" -> "FA". */
function initialsOf(fullName: string) {
  const parts = fullName.trim().split(/\s+/)
  const first = parts.at(0)?.[0] ?? ''
  const last = parts.length > 1 ? (parts.at(-1)?.[0] ?? '') : ''
  return (first + last).toUpperCase()
}

export function AppLayout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  async function handleLogout() {
    await logout()
    navigate('/login', { replace: true })
  }

  // The route guard guarantees a user here; narrowing keeps TypeScript honest.
  if (!user) return null

  const visibleItems = NAV_ITEMS.filter((item) => item.allow.includes(user.role))
  const currentItem = visibleItems.find((item) => location.pathname.startsWith(item.to))

  return (
    <SidebarProvider>
      <Sidebar collapsible="icon">
        <SidebarHeader>
          <div className="flex items-center gap-2 px-2 py-1.5">
            <FoundUMark decorative className="size-8 rounded-lg" />
            <div className="grid flex-1 text-left leading-tight group-data-[collapsible=icon]:hidden">
              <span className="truncate text-sm font-semibold">FoundU</span>
              <span className="truncate text-xs text-muted-foreground">Campus lost &amp; found</span>
            </div>
          </div>
        </SidebarHeader>

        <SidebarContent>
          <SidebarGroup>
            <SidebarGroupLabel>Workspace</SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                {visibleItems.map(({ to, label, icon: Icon }) => (
                  <SidebarMenuItem key={to}>
                    <SidebarMenuButton
                      isActive={location.pathname.startsWith(to)}
                      tooltip={label}
                      render={<NavLink to={to} />}
                    >
                      <Icon aria-hidden="true" />
                      <span>{label}</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                ))}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        </SidebarContent>

        <SidebarFooter>
          <SidebarMenu>
            <SidebarMenuItem>
              <DropdownMenu>
                <DropdownMenuTrigger
                  render={
                    <SidebarMenuButton size="lg" tooltip={user.fullName}>
                      <Avatar className="size-8 rounded-md">
                        <AvatarFallback className="rounded-md text-xs">
                          {initialsOf(user.fullName)}
                        </AvatarFallback>
                      </Avatar>
                      <div className="grid flex-1 text-left leading-tight">
                        <span className="truncate text-sm font-medium">{user.fullName}</span>
                        <span className="truncate text-xs text-muted-foreground">{user.role}</span>
                      </div>
                      <ChevronsUpDownIcon className="ml-auto size-4" aria-hidden="true" />
                    </SidebarMenuButton>
                  }
                />
                <DropdownMenuContent align="end" side="top" className="w-56">
                  {/* Base UI requires a group around a group label - unlike Radix, a bare
                      DropdownMenuLabel throws "MenuGroupContext is missing" at render. */}
                  <DropdownMenuGroup>
                    <DropdownMenuLabel className="font-normal">
                      <div className="grid leading-tight">
                        <span className="truncate text-sm font-medium">{user.fullName}</span>
                        <span className="truncate text-xs text-muted-foreground">{user.email}</span>
                      </div>
                    </DropdownMenuLabel>
                  </DropdownMenuGroup>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem onClick={handleLogout}>
                    <LogOutIcon aria-hidden="true" />
                    Sign out
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarFooter>

        <SidebarRail />
      </Sidebar>

      <SidebarInset>
        <header className="flex h-14 shrink-0 items-center gap-2 border-b px-4">
          <SidebarTrigger className="-ml-1" />
          <Separator orientation="vertical" className="mr-2 h-4" />
          <h1 className="text-sm font-medium">{currentItem?.label ?? 'FoundU'}</h1>
        </header>

        <main className="flex-1 p-6">
          <Outlet />
        </main>
      </SidebarInset>
    </SidebarProvider>
  )
}
