import { Link } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  InboxIcon,
  RotateCwIcon,
} from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider, PanelSheen } from '@/components/layout/dashboard-panel'
import { panelSurface } from '@/components/layout/panel-surface'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { FormSelect } from '@/features/reports/form-select'
import { timeAgo } from '@/features/feed/feed-api'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import { getClaimQueue } from './claims-api'
import { ClaimStatusChip } from './claim-status-chip'

const PAGE_SIZE = 15

/** Statuses worth filtering by. The terminal ones are grouped under "settled". */
const STATUS_OPTIONS = [
  { value: 'open', label: 'Needs attention' },
  { value: 'all', label: 'Everything' },
  { value: 'Pending', label: 'Submitted' },
  { value: 'WaitingForAnswer', label: 'Waiting on the claimant' },
  { value: 'UnderReview', label: 'Answered, to review' },
  { value: 'Approved', label: 'Approved' },
  { value: 'Rejected', label: 'Rejected' },
]

/**
 * The staff review queue.
 *
 * Oldest first, which the API decides - the claim that has waited longest is the one to work
 * next. "Needs attention" is the default because a queue that opens on everything buries the
 * three things somebody has to do today.
 */
export function ClaimQueuePage() {
  const [page, setPage] = useState(1)
  const [status, setStatus] = useState('open')

  const { data, isPending, isError, error, isFetching, refetch } = useQuery({
    queryKey: ['claim-queue', { page, status }],
    queryFn: () => getClaimQueue(page, PAGE_SIZE, status === 'open' || status === 'all' ? undefined : status),
    placeholderData: keepPreviousData,
  })

  // "Needs attention" is a view over statuses rather than one the API filters on, so it is
  // applied here: anything already settled drops out.
  const rows = (data?.items ?? []).filter((claim) =>
    status === 'open'
      ? !['Approved', 'Rejected', 'Cancelled'].includes(claim.status)
      : true,
  )

  return (
    <section className="flex flex-col gap-6">
      <DashboardPanel className="flex flex-col gap-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Claims</h1>
          <p className="pt-1 text-sm text-muted-foreground">
            Students who say an item in storage is theirs. Longest wait first.
          </p>
        </div>

        <PanelDivider />

        <div className="flex flex-col gap-2 sm:w-64">
          <Label htmlFor="claim-status">Show</Label>
          <FormSelect
            id="claim-status"
            value={status}
            onValueChange={(next) => {
              setStatus(next)
              setPage(1)
            }}
            options={STATUS_OPTIONS}
            placeholder="Needs attention"
          />
        </div>
      </DashboardPanel>

      {isPending ? (
        <div className="flex flex-col gap-3">
          {Array.from({ length: 4 }).map((_, index) => (
            <div key={index} className={cn(panelSurface, 'flex flex-col gap-3 p-5')}>
              <PanelSheen />
              <Skeleton className="h-4 w-52" />
              <Skeleton className="h-3 w-1/3" />
            </div>
          ))}
        </div>
      ) : isError ? (
        <DashboardPanel
          role="alert"
          className="flex flex-col items-start gap-3 border-destructive/40 from-destructive/8 via-destructive/5 to-transparent dark:from-destructive/15 dark:via-destructive/8"
        >
          <div>
            <p className="font-heading text-base font-medium">Could not load the queue</p>
            <p className="pt-1 text-sm text-muted-foreground">
              {error instanceof ApiError ? error.message : 'Check the API is running.'}
            </p>
          </div>
          <Button variant="outline" onClick={() => refetch()}>
            <RotateCwIcon aria-hidden="true" />
            Try again
          </Button>
        </DashboardPanel>
      ) : rows.length === 0 ? (
        <DashboardPanel className="flex flex-col items-center gap-3 py-16 text-center">
          <span className="flex size-12 items-center justify-center rounded-2xl bg-muted">
            <InboxIcon className="size-5 text-muted-foreground" aria-hidden="true" />
          </span>
          <p className="text-base font-medium">
            {status === 'open' ? 'Nothing waiting on the desk' : 'No claims match that filter'}
          </p>
          <p className="max-w-sm text-sm text-muted-foreground">
            {status === 'open'
              ? 'Every claim has been answered or decided.'
              : 'Try a different view.'}
          </p>
        </DashboardPanel>
      ) : (
        <>
          <ul className={cn('flex flex-col gap-3 transition-opacity duration-200', isFetching && 'opacity-60')}>
            {rows.map((claim) => (
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
                    <p className="pt-1 text-sm text-muted-foreground">
                      {claim.studentName} · {claim.categoryName}
                    </p>
                    <p className="pt-2 text-xs text-muted-foreground">
                      Waiting {timeAgo(claim.createdAt)}
                      {claim.unansweredQuestionCount > 0 &&
                        ` · ${claim.unansweredQuestionCount} question${claim.unansweredQuestionCount === 1 ? '' : 's'} unanswered`}
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

          {data.totalPages > 1 && (
            <nav
              aria-label="Claim pages"
              className={cn(panelSurface, 'flex items-center justify-between gap-4 p-3')}
            >
              <PanelSheen />
              <Button
                variant="outline"
                disabled={!data.hasPreviousPage}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                <ChevronLeftIcon aria-hidden="true" />
                Previous
              </Button>
              <span className="text-xs text-muted-foreground tabular-nums">
                {data.totalCount} claim{data.totalCount === 1 ? '' : 's'} · page {data.page} of{' '}
                {data.totalPages}
              </span>
              <Button
                variant="outline"
                disabled={!data.hasNextPage}
                onClick={() => setPage((p) => p + 1)}
              >
                Next
                <ChevronRightIcon aria-hidden="true" />
              </Button>
            </nav>
          )}
        </>
      )}
    </section>
  )
}
