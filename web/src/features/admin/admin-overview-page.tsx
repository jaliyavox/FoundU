import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import {
  ArrowRightIcon,
  BarChart3Icon,
  FlagIcon,
  GavelIcon,
  LifeBuoyIcon,
  PackageSearchIcon,
  UsersIcon,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider } from '@/components/layout/dashboard-panel'
import { Skeleton } from '@/components/ui/skeleton'
import { timeAgo } from '@/features/feed/feed-api'
import { api } from '@/lib/api/client'
import { useAuth } from '@/features/auth/use-auth'

/** Mirrors FoundU.Application.Admin.Dtos.AdminOverviewDto. */
interface AdminOverview {
  queues: {
    claimsAwaitingDecision: number
    claimsApprovedNotCollected: number
    itemsPostedAwaitingHandIn: number
    itemsUnclaimedInStorage: number
    flaggedReports: number
    supportOpen: number
    supportUnassigned: number
    oldestSupportHours: number
  }
  people: { students: number; staff: number; admins: number; suspended: number; newThisWeek: number }
  recent: { kind: string; summary: string; at: string }[]
}

const getOverview = () => api.get<AdminOverview>('/api/admin/overview')

/**
 * The admin panel's front page: everything waiting for a person, and where to go about it.
 *
 * Only actionable numbers live here - each one is a queue somebody can work through. Totals
 * that describe the past belong on analytics, which is one click away.
 */
export function AdminOverviewPage() {
  // Staff share this page, but moderation, users and analytics are Admin-only routes: they
  // see the figures without a link that would land on Forbidden.
  const isAdmin = useAuth().user?.role === 'Admin'
  const { data, isPending, isError, refetch } = useQuery({
    queryKey: ['admin-overview'],
    queryFn: getOverview,
    // Queues move while you are looking at them.
    refetchInterval: 60_000,
  })

  return (
    <section className="flex w-full flex-col gap-6">
      <div>
        <p className="text-sm font-medium text-brand-green">Admin</p>
        <h1 className="pt-1 text-2xl font-semibold tracking-tight">What needs a person today</h1>
        <p className="max-w-xl pt-2 text-sm text-muted-foreground">
          Every number here is a queue somebody can work through. The figures that describe the
          past live on analytics.
        </p>
      </div>

      {isPending ? (
        <>
          <Skeleton className="h-40 w-full" />
          <Skeleton className="h-56 w-full" />
        </>
      ) : isError ? (
        <DashboardPanel className="flex flex-col items-start gap-3" role="alert">
          <p className="text-sm">Could not load the overview.</p>
          <Button variant="outline" size="sm" onClick={() => refetch()}>Try again</Button>
        </DashboardPanel>
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <QueueCard
              to="/claims"
              icon={<GavelIcon className="size-4" aria-hidden="true" />}
              value={data.queues.claimsAwaitingDecision}
              label="claims awaiting a decision"
              note={`${data.queues.claimsApprovedNotCollected} approved and not collected`}
            />
            <QueueCard
              to="/admin/support"
              icon={<LifeBuoyIcon className="size-4" aria-hidden="true" />}
              value={data.queues.supportOpen}
              label="support tickets with us"
              note={
                data.queues.supportOpen === 0
                  ? 'Nothing waiting'
                  : `${data.queues.supportUnassigned} unpicked · longest wait ${data.queues.oldestSupportHours}h`
              }
            />
            <QueueCard
              to="/items"
              icon={<PackageSearchIcon className="size-4" aria-hidden="true" />}
              value={data.queues.itemsUnclaimedInStorage}
              label="items in storage"
              note={`${data.queues.itemsPostedAwaitingHandIn} posted but not handed in`}
            />
            <QueueCard
              to={isAdmin ? '/admin/moderation' : undefined}
              icon={<FlagIcon className="size-4" aria-hidden="true" />}
              value={data.queues.flaggedReports}
              label="flagged reports"
              note={data.queues.flaggedReports === 0 ? 'Nothing flagged' : 'Needs a look'}
            />
            <QueueCard
              to={isAdmin ? '/admin' : undefined}
              icon={<UsersIcon className="size-4" aria-hidden="true" />}
              value={data.people.students + data.people.staff + data.people.admins}
              label="accounts"
              note={`${data.people.newThisWeek} new this week · ${data.people.suspended} suspended`}
            />
            <QueueCard
              to={isAdmin ? '/admin/analytics' : undefined}
              icon={<BarChart3Icon className="size-4" aria-hidden="true" />}
              value={data.people.staff + data.people.admins}
              label="people on the desk"
              note="Open analytics"
            />
          </div>

          <DashboardPanel className="flex flex-col gap-4">
            <h2 className="font-heading text-base font-medium">Just happened</h2>
            <PanelDivider />
            {data.recent.length === 0 ? (
              <p className="text-sm text-muted-foreground">Nothing yet today.</p>
            ) : (
              <ol className="flex flex-col">
                {data.recent.map((entry, index) => (
                  <li
                    key={`${entry.kind}-${entry.at}-${index}`}
                    className={index === 0 ? 'flex items-baseline justify-between gap-3 pb-3' : 'flex items-baseline justify-between gap-3 border-t border-foreground/8 py-3'}
                  >
                    <span className="text-sm">{entry.summary}</span>
                    <span className="shrink-0 text-xs text-muted-foreground">{timeAgo(entry.at)}</span>
                  </li>
                ))}
              </ol>
            )}
          </DashboardPanel>
        </>
      )}
    </section>
  )
}

function QueueCard({
  to,
  icon,
  value,
  label,
  note,
}: {
  to?: string
  icon: React.ReactNode
  value: number
  label: string
  note: string
}) {
  return (
    <DashboardPanel className="flex flex-col gap-2">
      <span className="flex items-center gap-2 text-muted-foreground">{icon}</span>
      <p className="text-4xl font-semibold tracking-tight tabular-nums">{value}</p>
      <p className="text-sm">{label}</p>
      <p className="text-xs text-muted-foreground">{note}</p>
      {to && <Button
        variant="ghost"
        size="sm"
        nativeButton={false}
        render={<Link to={to} />}
        className="mt-1 self-start px-0 text-brand-forest hover:bg-transparent hover:underline dark:text-brand-sage"
      >
        Open<ArrowRightIcon aria-hidden="true" />
      </Button>}
    </DashboardPanel>
  )
}
