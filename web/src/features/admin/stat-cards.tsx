import { useQuery } from '@tanstack/react-query'
import { ShieldOffIcon, TrendingUpIcon, UserCogIcon, UsersIcon } from 'lucide-react'
import { PanelSheen } from '@/components/layout/dashboard-panel'
import { panelSurface } from '@/components/layout/panel-surface'
import { Skeleton } from '@/components/ui/skeleton'
import { getUserStats } from './admin-api'
import { cn } from '@/lib/utils'

/**
 * Headline counts. Real figures from a single grouped query - nothing here is invented,
 * which matters on a dashboard where a made-up number would be indistinguishable from a
 * real one.
 */
export function StatCards() {
  const { data, isPending, isError } = useQuery({
    queryKey: ['admin-user-stats'],
    queryFn: getUserStats,
  })

  if (isPending) {
    return (
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {Array.from({ length: 4 }).map((_, index) => (
          <div key={index} className={cn(panelSurface, 'p-5')}>
            <PanelSheen />
            <Skeleton className="h-3.5 w-24" />
            <Skeleton className="mt-3 h-9 w-16" />
            <Skeleton className="mt-3 h-3 w-32" />
          </div>
        ))}
      </div>
    )
  }

  // A failed count should not take the users table down with it.
  if (isError) return null

  const cards = [
    {
      label: 'Total accounts',
      value: data.totalUsers,
      hint: `${data.joinedLast30Days} joined in the last 30 days`,
      icon: UsersIcon,
      featured: true,
    },
    { label: 'Students', value: data.students, hint: 'Can report lost items', icon: UsersIcon },
    { label: 'Staff & admins', value: data.staff + data.admins, hint: 'Can log found items', icon: UserCogIcon },
    {
      label: 'Suspended',
      value: data.suspended,
      hint: data.suspended === 0 ? 'Nobody is locked out' : 'Blocked from signing in',
      icon: ShieldOffIcon,
    },
  ]

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {cards.map(({ label, value, hint, icon: Icon, featured }) => (
        <div
          key={label}
          className={cn(
            panelSurface,
            'p-5 transition-shadow duration-300 hover:shadow-md',
            // The headline count keeps the brand slab, so one card leads the row - but it
            // wears the same stroke and rounding as the rest.
            featured &&
              'border-brand-forest/60 from-brand-forest via-brand-forest to-[oklch(0.32_0.09_144)] text-white dark:border-brand-forest/60 dark:from-brand-forest dark:via-brand-forest dark:to-[oklch(0.32_0.09_144)]',
          )}
        >
          <PanelSheen className={cn(featured && 'via-white/45')} />

          {featured && (
            <div
              aria-hidden="true"
              className="pointer-events-none absolute -top-16 -right-10 size-40 rounded-full bg-brand-green/25 blur-2xl"
            />
          )}

          <div className="relative">
            <div className="flex items-center justify-between">
              <p className={cn('text-sm', featured ? 'text-white/70' : 'text-muted-foreground')}>
                {label}
              </p>
              <Icon
                className={cn('size-4', featured ? 'text-white/60' : 'text-muted-foreground')}
                aria-hidden="true"
              />
            </div>

            <p className="pt-2 text-3xl font-semibold tracking-tight tabular-nums">{value}</p>

            <p
              className={cn(
                'flex items-center gap-1.5 pt-2 text-xs',
                featured ? 'text-white/60' : 'text-muted-foreground',
              )}
            >
              {featured && data.joinedLast30Days > 0 && (
                <TrendingUpIcon className="size-3 text-brand-sage" aria-hidden="true" />
              )}
              {hint}
            </p>
          </div>
        </div>
      ))}
    </div>
  )
}
