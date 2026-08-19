import { useQuery } from '@tanstack/react-query'
import { ShieldOffIcon, TrendingUpIcon, UserCogIcon, UsersIcon } from 'lucide-react'
import { Card, CardContent } from '@/components/ui/card'
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
          <Card key={index}>
            <CardContent className="pt-6">
              <Skeleton className="h-3.5 w-24" />
              <Skeleton className="mt-3 h-9 w-16" />
              <Skeleton className="mt-3 h-3 w-32" />
            </CardContent>
          </Card>
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
        <Card
          key={label}
          className={cn(
            'relative overflow-hidden bg-linear-to-b from-card to-muted/40 transition-shadow duration-300 hover:shadow-md',
            featured && 'border-transparent bg-linear-to-br from-brand-forest to-[oklch(0.32_0.09_144)] text-white',
          )}
        >
          {featured && (
            <div
              aria-hidden="true"
              className="pointer-events-none absolute -top-16 -right-10 size-40 rounded-full bg-brand-green/25 blur-2xl"
            />
          )}

          <CardContent className="relative pt-6">
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
          </CardContent>
        </Card>
      ))}
    </div>
  )
}
