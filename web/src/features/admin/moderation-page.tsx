import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  CheckIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  FlagIcon,
  GavelIcon,
  Loader2Icon,
  RotateCwIcon,
} from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider } from '@/components/layout/dashboard-panel'
import { Skeleton } from '@/components/ui/skeleton'
import { getClaimQueue } from '@/features/claims/claims-api'
import { timeAgo } from '@/features/feed/feed-api'
import { ApiError } from '@/lib/api/client'
import { clearFlag, getFlaggedReports } from './admin-api'

/**
 * The two things an administrator gets asked to look at: reports somebody flagged, and
 * claims that were rejected and may be disputed. Both lists are oldest first - the one that
 * has waited longest is the one to open.
 */
export function ModerationPage() {
  return (
    <section className="flex flex-col gap-6">
      <DashboardPanel>
        <h1 className="text-2xl font-semibold tracking-tight">Moderation</h1>
        <p className="pt-1 text-sm text-muted-foreground">
          Reports that were flagged, and rejections that may need a second look.
        </p>
      </DashboardPanel>

      <FlaggedReports />
      <RejectedClaims />
    </section>
  )
}

function FlaggedReports() {
  const queryClient = useQueryClient()
  const [page, setPage] = useState(1)

  const { data, isPending, isError, error, refetch } = useQuery({
    queryKey: ['flagged-reports', page],
    queryFn: () => getFlaggedReports(page),
    placeholderData: keepPreviousData,
  })

  // Clearing the last flag on a later page would leave it empty; step back instead.
  useEffect(() => {
    if (data && page > 1 && page > data.totalPages) setPage(Math.max(1, data.totalPages))
  }, [data, page])

  const clear = useMutation({
    mutationFn: clearFlag,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['flagged-reports'] })
      queryClient.invalidateQueries({ queryKey: ['admin-analytics'] })
      toast.success('Flag cleared.')
    },
    onError: (err) => toast.error(err instanceof ApiError ? err.message : 'Could not reach the server.'),
  })

  return (
    <DashboardPanel className="flex flex-col gap-4">
      <div className="flex items-center gap-2">
        <FlagIcon className="size-4 text-muted-foreground" aria-hidden="true" />
        <h2 className="font-heading text-base font-medium">Flagged reports</h2>
        {data && data.totalCount > 0 && (
          <span className="text-sm text-muted-foreground tabular-nums">· {data.totalCount}</span>
        )}
      </div>

      <PanelDivider />

      {isPending ? (
        <div className="flex flex-col gap-3">
          <Skeleton className="h-4 w-2/3" />
          <Skeleton className="h-4 w-1/2" />
        </div>
      ) : isError ? (
        <div className="flex flex-col items-start gap-3">
          <p className="text-sm text-muted-foreground">
            {error instanceof ApiError ? error.message : 'Could not load the flags.'}
          </p>
          <Button variant="outline" size="sm" onClick={() => refetch()}>
            <RotateCwIcon aria-hidden="true" />
            Try again
          </Button>
        </div>
      ) : data.items.length === 0 ? (
        <p className="text-sm text-muted-foreground">Nothing flagged. Nothing needs a look.</p>
      ) : (
        <ul className="flex flex-col gap-3">
          {data.items.map((report) => (
            <li
              key={report.id}
              className="flex flex-col gap-3 rounded-xl border border-foreground/8 bg-background/50 p-4 sm:flex-row sm:items-start sm:justify-between"
            >
              <div className="min-w-0 flex-1">
                <p className="font-medium">
                  {report.itemTypeName}
                  <span className="pl-2 text-xs font-normal text-muted-foreground">{report.categoryName}</span>
                </p>
                <p className="truncate text-sm text-muted-foreground">{report.description}</p>
                <p className="pt-2 border-l-2 border-amber-500/50 pl-3 text-sm text-pretty">
                  {report.flagReason}
                </p>
                <p className="pt-1 text-xs text-muted-foreground">
                  Flagged by {report.flaggedByName ?? 'someone'}
                  {report.flaggedAt && ` · ${timeAgo(report.flaggedAt)}`}
                </p>
              </div>

              <Button
                variant="outline"
                size="sm"
                className="shrink-0"
                onClick={() => clear.mutate(report.id)}
                disabled={clear.isPending && clear.variables === report.id}
              >
                {clear.isPending && clear.variables === report.id ? (
                  <Loader2Icon className="animate-spin" aria-hidden="true" />
                ) : (
                  <CheckIcon aria-hidden="true" />
                )}
                Looked at it
              </Button>
            </li>
          ))}
        </ul>
      )}

      {data && data.totalPages > 1 && (
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span className="tabular-nums">
            Page {data.page} of {data.totalPages}
          </span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={!data.hasPreviousPage} onClick={() => setPage(p => p - 1)}>
              <ChevronLeftIcon aria-hidden="true" />
              Previous
            </Button>
            <Button variant="outline" size="sm" disabled={!data.hasNextPage} onClick={() => setPage(p => p + 1)}>
              Next
              <ChevronRightIcon aria-hidden="true" />
            </Button>
          </div>
        </div>
      )}
    </DashboardPanel>
  )
}

function RejectedClaims() {
  const { data, isPending, isError, error, refetch } = useQuery({
    queryKey: ['claim-queue', { page: 1, status: 'Rejected', pageSize: 20 }],
    queryFn: () => getClaimQueue(1, 20, 'Rejected'),
  })

  return (
    <DashboardPanel className="flex flex-col gap-4">
      <div>
        <div className="flex items-center gap-2">
          <GavelIcon className="size-4 text-muted-foreground" aria-hidden="true" />
          <h2 className="font-heading text-base font-medium">Rejected claims</h2>
          {data && data.totalCount > 0 && (
            <span className="text-sm text-muted-foreground tabular-nums">· {data.totalCount}</span>
          )}
        </div>
        <p className="pt-1 text-sm text-muted-foreground">
          A rejection can be overturned from the claim itself, if the item is still in storage.
          The original decision stays on record next to yours.
        </p>
      </div>

      <PanelDivider />

      {isPending ? (
        <div className="flex flex-col gap-3">
          <Skeleton className="h-4 w-2/3" />
          <Skeleton className="h-4 w-1/2" />
        </div>
      ) : isError ? (
        <div className="flex flex-col items-start gap-3">
          <p className="text-sm text-muted-foreground">
            {error instanceof ApiError ? error.message : 'Could not load the claims.'}
          </p>
          <Button variant="outline" size="sm" onClick={() => refetch()}>
            <RotateCwIcon aria-hidden="true" />
            Try again
          </Button>
        </div>
      ) : data.items.length === 0 ? (
        <p className="text-sm text-muted-foreground">No rejected claims.</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {data.items.map((claim) => (
            <li key={claim.id}>
              <Link
                to={`/claims/${claim.id}`}
                className="flex items-center justify-between gap-3 rounded-xl border border-foreground/8 bg-background/50 p-4 transition-colors hover:bg-muted/50 focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
              >
                <div className="min-w-0">
                  <p className="font-medium">
                    {claim.itemTypeName}
                    <span className="pl-2 text-xs font-normal text-muted-foreground">{claim.categoryName}</span>
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {claim.studentName} · rejected {timeAgo(claim.updatedAt)}
                  </p>
                </div>
                <span className="shrink-0 text-sm text-muted-foreground">Review</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </DashboardPanel>
  )
}
