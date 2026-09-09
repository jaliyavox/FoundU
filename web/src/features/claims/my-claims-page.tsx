import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ChevronRightIcon, RotateCwIcon, ShieldQuestionIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelSheen } from '@/components/layout/dashboard-panel'
import { panelSurface } from '@/components/layout/panel-surface'
import { Skeleton } from '@/components/ui/skeleton'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import { timeAgo } from '@/features/feed/feed-api'
import { CLAIM_STATUS_COPY, getMyClaims } from './claims-api'
import { ClaimStatusChip } from './claim-status-chip'
import { SuggestionsPanel } from './suggestions-panel'

/** Everything the student has claimed, and what each one is waiting on. */
export function MyClaimsPage() {
  const { data, isPending, isError, error, refetch } = useQuery({
    queryKey: ['my-claims'],
    queryFn: () => getMyClaims(),
  })

  return (
    <section className="flex flex-col gap-6">
      <DashboardPanel>
        <h1 className="text-2xl font-semibold tracking-tight">My claims</h1>
        <p className="pt-1 text-sm text-muted-foreground">
          Items you have said are yours, and where each one has got to.
        </p>
      </DashboardPanel>

      <SuggestionsPanel />

      {isPending ? (
        <div className="flex flex-col gap-3">
          {Array.from({ length: 2 }).map((_, index) => (
            <div key={index} className={cn(panelSurface, 'flex flex-col gap-3 p-5')}>
              <PanelSheen />
              <Skeleton className="h-4 w-44" />
              <Skeleton className="h-3 w-2/3" />
            </div>
          ))}
        </div>
      ) : isError ? (
        <DashboardPanel
          role="alert"
          className="flex flex-col items-start gap-3 border-destructive/40 from-destructive/8 via-destructive/5 to-transparent dark:from-destructive/15 dark:via-destructive/8"
        >
          <div>
            <p className="font-heading text-base font-medium">Could not load your claims</p>
            <p className="pt-1 text-sm text-muted-foreground">
              {error instanceof ApiError ? error.message : 'Check the API is running.'}
            </p>
          </div>
          <Button variant="outline" onClick={() => refetch()}>
            <RotateCwIcon aria-hidden="true" />
            Try again
          </Button>
        </DashboardPanel>
      ) : data.items.length === 0 ? (
        <DashboardPanel className="flex flex-col items-center gap-3 py-16 text-center">
          <span className="flex size-12 items-center justify-center rounded-2xl bg-muted">
            <ShieldQuestionIcon className="size-5 text-muted-foreground" aria-hidden="true" />
          </span>
          <p className="text-base font-medium">You have not claimed anything</p>
          <p className="max-w-sm text-sm text-muted-foreground">
            When staff match something handed in against one of your reports, it appears here for
            you to confirm.
          </p>
          <Button variant="outline" className="mt-1" nativeButton={false} render={<Link to="/my-reports" />}>
            Go to my reports
          </Button>
        </DashboardPanel>
      ) : (
        <ul className="flex flex-col gap-3">
          {data.items.map((claim) => (
            <li key={claim.id}>
              <Link
                to={`/claims/${claim.id}`}
                className={cn(
                  panelSurface,
                  'group flex items-center gap-4 p-5 transition-shadow duration-300 hover:shadow-md focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none',
                )}
              >
                <PanelSheen />

                <div className="relative min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="font-medium">{claim.itemTypeName}</h2>
                    <ClaimStatusChip status={claim.status} />
                  </div>
                  <p className="pt-1 text-sm text-pretty text-muted-foreground">
                    {CLAIM_STATUS_COPY[claim.status].student}
                  </p>
                  <p className="pt-2 text-xs text-muted-foreground">
                    Claimed {timeAgo(claim.createdAt)}
                    {claim.unansweredQuestionCount > 0 &&
                      ` · ${claim.unansweredQuestionCount} question${claim.unansweredQuestionCount === 1 ? '' : 's'} waiting on you`}
                  </p>
                </div>

                <ChevronRightIcon
                  className="relative size-4 shrink-0 text-muted-foreground transition-transform duration-300 group-hover:translate-x-0.5"
                  aria-hidden="true"
                />
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
