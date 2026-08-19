import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  ClockIcon,
  FileTextIcon,
  Loader2Icon,
  MapPinIcon,
  PlusIcon,
  RotateCwIcon,
} from 'lucide-react'
import { toast } from 'sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import {
  formatDateTime,
  getMyLostReports,
  withdrawLostReport,
  type LostReportListItem,
} from './reports-api'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'

const PAGE_SIZE = 10

/** Only an Active report can be withdrawn - the API rejects the rest with a 409. */
const STATUS_STYLES: Record<string, string> = {
  Active: 'bg-brand-green/15 text-brand-forest dark:text-brand-sage',
  Matched: 'bg-primary/15 text-primary',
  Resolved: 'bg-muted text-muted-foreground',
  Withdrawn: 'bg-muted text-muted-foreground',
}

export function MyReportsPage() {
  const [page, setPage] = useState(1)
  const queryClient = useQueryClient()

  const { data, isPending, isError, error, refetch } = useQuery({
    queryKey: ['my-lost-reports', { page }],
    queryFn: () => getMyLostReports(page, PAGE_SIZE),
  })

  const withdraw = useMutation({
    mutationFn: (id: string) => withdrawLostReport(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['my-lost-reports'] })
      queryClient.invalidateQueries({ queryKey: ['lost-feed'] })
      toast.success('Report withdrawn. It no longer appears on the feed.')
    },
    onError: (mutationError) => {
      toast.error(
        mutationError instanceof ApiError
          ? mutationError.message
          : 'Could not withdraw the report.',
      )
    },
  })

  return (
    <section className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">My reports</h1>
          <p className="pt-1 text-sm text-muted-foreground">
            Everything you have reported lost, and where it has got to.
          </p>
        </div>

        <Button nativeButton={false} render={<Link to="/my-reports/new" />}>
          <PlusIcon aria-hidden="true" />
          Report a lost item
        </Button>
      </div>

      {isPending ? (
        <div className="flex flex-col gap-3">
          {Array.from({ length: 3 }).map((_, index) => (
            <Card key={index}>
              <CardContent className="flex flex-col gap-3 pt-6">
                <Skeleton className="h-4 w-40" />
                <Skeleton className="h-3 w-full" />
                <Skeleton className="h-3 w-2/3" />
              </CardContent>
            </Card>
          ))}
        </div>
      ) : isError ? (
        <Card role="alert" className="border-destructive/40 bg-destructive/5">
          <CardHeader>
            <CardTitle className="text-base">Could not load your reports</CardTitle>
            <CardDescription>
              {error instanceof ApiError ? error.message : 'Check the API is running.'}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button variant="outline" onClick={() => refetch()}>
              <RotateCwIcon aria-hidden="true" />
              Try again
            </Button>
          </CardContent>
        </Card>
      ) : data.items.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-16 text-center">
            <span className="flex size-12 items-center justify-center rounded-2xl bg-muted">
              <FileTextIcon className="size-5 text-muted-foreground" aria-hidden="true" />
            </span>
            <p className="text-base font-medium">You have not reported anything lost</p>
            <p className="max-w-sm text-sm text-muted-foreground">
              If you lose something on campus, report it here and we will compare it against
              everything handed in.
            </p>
            <Button className="mt-1" nativeButton={false} render={<Link to="/my-reports/new" />}>
              <PlusIcon aria-hidden="true" />
              Report a lost item
            </Button>
          </CardContent>
        </Card>
      ) : (
        <>
          <ul className="flex flex-col gap-3">
            {data.items.map((report) => (
              <li key={report.id}>
                <ReportCard
                  report={report}
                  onWithdraw={() => withdraw.mutate(report.id)}
                  isWithdrawing={withdraw.isPending && withdraw.variables === report.id}
                />
              </li>
            ))}
          </ul>

          {data.totalPages > 1 && (
            <nav aria-label="Report pages" className="flex items-center justify-between gap-4">
              <Button
                variant="outline"
                disabled={!data.hasPreviousPage}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                <ChevronLeftIcon aria-hidden="true" />
                Previous
              </Button>
              <span className="text-xs text-muted-foreground tabular-nums">
                Page {data.page} of {data.totalPages}
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

function ReportCard({
  report,
  onWithdraw,
  isWithdrawing,
}: {
  report: LostReportListItem
  onWithdraw: () => void
  isWithdrawing: boolean
}) {
  return (
    <Card>
      <CardContent className="flex flex-col gap-3 pt-6 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-medium">{report.itemTypeName}</span>
            <Badge
              variant="secondary"
              className={cn(STATUS_STYLES[report.status] ?? 'bg-muted text-muted-foreground')}
            >
              {report.status}
            </Badge>
            {report.primaryColor && <Badge variant="secondary">{report.primaryColor}</Badge>}
          </div>

          <p className="pt-2 text-sm text-pretty text-muted-foreground">{report.description}</p>

          <div className="flex flex-wrap gap-x-4 gap-y-1 pt-3 text-xs text-muted-foreground">
            <span className="flex items-center gap-1.5">
              <MapPinIcon className="size-3.5" aria-hidden="true" />
              {report.lastSeenLocationName}
            </span>
            <span className="flex items-center gap-1.5">
              <ClockIcon className="size-3.5" aria-hidden="true" />
              {formatDateTime(report.estimatedLostFromAt)} - {formatDateTime(report.estimatedLostToAt)}
            </span>
          </div>
        </div>

        {report.status === 'Active' && (
          <Button
            variant="outline"
            size="sm"
            onClick={onWithdraw}
            disabled={isWithdrawing}
            className="shrink-0"
          >
            {isWithdrawing && <Loader2Icon className="animate-spin" aria-hidden="true" />}
            Withdraw
          </Button>
        )}
      </CardContent>
    </Card>
  )
}
