import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import {
  ChevronsUpDownIcon,
  FileTextIcon,
  LogOutIcon,
  MoonIcon,
  PackageSearchIcon,
  ShieldIcon,
  SunIcon,
} from 'lucide-react'
import { FoundUMark } from '@/components/brand/foundu-logo'
import { FeedLink } from './feed-link'
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
import { Button } from '@/components/ui/button'
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
import { useDashboardTheme } from '@/hooks/use-dashboard-theme'
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
  const { theme, toggle } = useDashboardTheme()
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
      {/* Pitch-black rail in dark mode only - light mode keeps the standard light sidebar.
          The tokens are overridden as dark:-prefixed custom properties rather than an inline
          style, because an inline style cannot be scoped to a theme. Overriding the tokens
          (rather than the background) means every SidebarMenuButton hover, active and ring
          state follows without restyling each one. */}
      <Sidebar
        collapsible="icon"
        variant="inset"
        className="[&_[data-slot=sidebar-inner]]:relative [&_[data-slot=sidebar-inner]]:overflow-hidden dark:[--sidebar-accent-foreground:oklch(0.98_0.008_150)] dark:[--sidebar-accent:oklch(1_0_0_/_8%)] dark:[--sidebar-border:oklch(1_0_0_/_10%)] dark:[--sidebar-foreground:oklch(0.96_0.01_150)] dark:[--sidebar-primary-foreground:oklch(0.16_0.028_148)] dark:[--sidebar-primary:oklch(0.721_0.141_146.1)] dark:[--sidebar-ring:oklch(0.721_0.141_146.1)] dark:[--sidebar:oklch(0.04_0.004_150)]"
      >
        {/* Blooms are dark-mode only. In light mode the rail is flat black, which reads as
            a deliberate slab against the pale page rather than a smudged one. */}
        <div aria-hidden="true" className="pointer-events-none absolute inset-0 hidden dark:block">
          <div className="fu-aurora-a absolute -top-24 -left-16 size-72 rounded-full bg-brand-forest/50 blur-[90px]" />
          <div className="fu-aurora-b absolute top-1/2 -right-16 size-64 rounded-full bg-brand-green/20 blur-[90px]" />
          <div className="fu-aurora-c absolute -bottom-20 left-0 size-60 rounded-full bg-brand-forest/40 blur-[90px]" />
        </div>

        <SidebarHeader className="relative">
          {/* The mark is the way back out to the public site, the way it is on the landing
              page and the auth screens. */}
          <Link
            to="/"
            aria-label="FoundU home"
            className="flex items-center gap-2 rounded-lg px-2 py-1.5 transition-colors hover:bg-sidebar-accent focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
          >
            <FoundUMark decorative className="size-8 rounded-lg" />
            <div className="grid flex-1 text-left leading-tight group-data-[collapsible=icon]:hidden">
              <span className="truncate text-sm font-semibold">FoundU</span>
              <span className="truncate text-xs text-muted-foreground">Campus lost &amp; found</span>
            </div>
          </Link>
        </SidebarHeader>

        <SidebarContent className="relative">
          <SidebarGroup>
            <SidebarGroupLabel className="text-xs tracking-wider uppercase">Workspace</SidebarGroupLabel>
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

        <SidebarFooter className="relative">
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
        {/* Not an <h1>: every page renders its own, and two per document breaks the
            heading outline. This is a location label, so it is plain text. */}
        <header className="sticky top-0 z-10 flex h-16 shrink-0 items-center gap-2 border-b bg-background/70 px-4 backdrop-blur-md sm:px-6">
          <SidebarTrigger className="-ml-1" />
          <Separator orientation="vertical" className="mr-1 h-4" />
          <p className="text-sm font-medium text-muted-foreground">
            {currentItem?.label ?? 'FoundU'}
          </p>

          {/* The feed is public rather than part of the role-gated workspace, so it sits in
              the header instead of among the sidebar sections. */}
          <FeedLink />

          <Button
            variant="ghost"
            size="icon-sm"
            onClick={toggle}
            aria-label={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
            title={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
          >
            {theme === 'dark' ? (
              <SunIcon className="size-4" aria-hidden="true" />
            ) : (
              <MoonIcon className="size-4" aria-hidden="true" />
            )}
          </Button>
        </header>

        {/* SidebarInset is itself a <main>, so this must not be one too. */}
        <div className="relative flex-1 bg-linear-to-b from-background to-muted/50">
          {/* Same drifting blooms as the landing sections, dialled well down: this sits
              behind working content, not a hero. The decorative layer clips its own blobs
              so the page still scrolls normally. */}
          <div aria-hidden="true" className="pointer-events-none absolute inset-0 overflow-hidden">
            <div className="fu-aurora-a absolute -top-40 left-[8%] size-[30rem] rounded-full bg-brand-sage/25 blur-[130px] dark:bg-brand-forest/45" />
            <div className="fu-aurora-b absolute top-1/3 -right-20 size-[24rem] rounded-full bg-brand-green/12 blur-[130px] dark:bg-brand-green/18" />
            <div className="fu-aurora-c absolute bottom-0 left-1/3 size-[22rem] rounded-full bg-brand-mist/60 blur-[130px] dark:bg-brand-green/8" />
          </div>

          <div className="relative p-4 sm:p-6">
            <Outlet />
          </div>
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}
