import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  BellRingIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  FileTextIcon,
  Loader2Icon,
  PlusIcon,
  RotateCwIcon,
} from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelSheen } from '@/components/layout/dashboard-panel'
import { panelSurface } from '@/components/layout/panel-surface'
import { Skeleton } from '@/components/ui/skeleton'
import {
  getMyLostReports,
  withdrawLostReport,
  type LostReportListItem,
} from './reports-api'
import { timeAgo } from '@/features/feed/feed-api'
import { ItemIllustration } from '@/features/feed/item-illustration'
import { ItemMedia } from '@/features/feed/item-media'
import { WithdrawDialog } from './withdraw-dialog'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'

const PAGE_SIZE = 10


export function MyReportsPage() {
  const [page, setPage] = useState(1)
  const [withdrawTarget, setWithdrawTarget] = useState<LostReportListItem | null>(null)
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
      setWithdrawTarget(null)
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
      <DashboardPanel className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">My reports</h1>
          <p className="pt-1 text-sm text-muted-foreground">
            Everything you have reported lost, and where it has got to.
          </p>
        </div>

        {/* Inverted against the page: black on the light theme, white on the dark one, so
            the primary action is the highest-contrast thing on screen either way. */}
        <Button
          className="bg-neutral-900 text-white hover:bg-neutral-900/90 dark:bg-white dark:text-neutral-900 dark:hover:bg-white/90"
          nativeButton={false}
          render={<Link to="/my-reports/new" />}
        >
          <PlusIcon aria-hidden="true" />
          Report a lost item
        </Button>
      </DashboardPanel>

      {isPending ? (
        <div className="flex flex-col gap-3">
          {Array.from({ length: 3 }).map((_, index) => (
            <div key={index} className={cn(panelSurface, 'flex flex-col gap-3 p-5')}>
              <PanelSheen />
              <Skeleton className="h-4 w-40" />
              <Skeleton className="h-3 w-full" />
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
            <p className="font-heading text-base font-medium">Could not load your reports</p>
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
            <FileTextIcon className="size-5 text-muted-foreground" aria-hidden="true" />
          </span>
          <p className="text-base font-medium">You have not reported anything lost</p>
          <p className="max-w-sm text-sm text-muted-foreground">
            If you lose something on campus, report it here and we will compare it against
            everything handed in.
          </p>
          <Button
            className="mt-1 bg-neutral-900 text-white hover:bg-neutral-900/90 dark:bg-white dark:text-neutral-900 dark:hover:bg-white/90"
            nativeButton={false}
            render={<Link to="/my-reports/new" />}
          >
            <PlusIcon aria-hidden="true" />
            Report a lost item
          </Button>
        </DashboardPanel>
      ) : (
        <>
          <ul className="flex flex-col gap-3">
            {data.items.map((report) => (
              <li key={report.id}>
                <ReportCard
                  report={report}
                  onWithdraw={() => setWithdrawTarget(report)}
                  isWithdrawing={withdraw.isPending && withdraw.variables === report.id}
                />
              </li>
            ))}
          </ul>

          {data.totalPages > 1 && (
            <nav
              aria-label="Report pages"
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

      <WithdrawDialog
        report={withdrawTarget}
        onConfirm={() => withdrawTarget && withdraw.mutate(withdrawTarget.id)}
        onClose={() => setWithdrawTarget(null)}
        isWithdrawing={withdraw.isPending}
      />
    </section>
  )
}

/**
 * Where a report sits in its life. Withdrawn is off this path, not a stage of it.
 *
 * Every stage is read off real data - none of them is decorative:
 *  - Reported          the report exists and is Active
 *  - Someone found it  a finder has written to you, which is all a message here can mean
 *  - At the guard desk staff matched a logged found item to your report, so it is in storage
 *  - Returned          the report is Resolved
 */
const LIFECYCLE = ['Reported', 'Someone found it', 'At the guard desk', 'Returned'] as const

/** Knob position per stage. The ends stop short of the edges so they stay dots on a track
 *  rather than caps on it. */
const STAGE_OFFSET = ['3%', '35%', '67%', '97%']

/** Which end the pill hangs from, so it never runs off a narrow card. */
const PILL_ALIGN = ['left-0', '-translate-x-1/2', '-translate-x-1/2', 'right-0'] as const

function stageOf(report: LostReportListItem) {
  if (report.status === 'Resolved') return 3
  if (report.status === 'Matched') return 2
  // Either signal counts: pressing "I found this" is recorded on its own, and a message is
  // only ever sent by someone saying the same thing.
  return report.foundClaimCount > 0 || report.messageCount > 0 ? 1 : 0
}

/** Largest whole unit since the report went up, as a number and its word. */
function elapsedSince(iso: string) {
  const minutes = Math.max(0, (Date.now() - new Date(iso).getTime()) / 60000)

  if (minutes < 60) {
    const value = Math.max(1, Math.round(minutes))
    return { value, unit: value === 1 ? 'minute' : 'minutes' }
  }

  if (minutes < 60 * 24) {
    const value = Math.round(minutes / 60)
    return { value, unit: value === 1 ? 'hour' : 'hours' }
  }

  const value = Math.round(minutes / (60 * 24))
  return { value, unit: value === 1 ? 'day' : 'days' }
}

const shortDate = (iso: string) =>
  new Date(iso).toLocaleDateString('en', { day: 'numeric', month: 'short' })

/**
 * One report, as a status card rather than a paragraph: the item's own illustration, the
 * time it has been open, and where it sits on the path from reported to returned. Anything
 * that can be read off the track is not also written out in words.
 */
function ReportCard({
  report,
  onWithdraw,
  isWithdrawing,
}: {
  report: LostReportListItem
  onWithdraw: () => void
  isWithdrawing: boolean
}) {
  const isWithdrawn = report.status === 'Withdrawn'
  const stage = stageOf(report)
  const { value, unit } = elapsedSince(report.createdAt)

  return (
    <article
      className={cn(
        panelSurface,
        'group p-5 transition-shadow duration-300 hover:shadow-md sm:p-6',
        isWithdrawn && 'opacity-75',
      )}
    >
      <PanelSheen />

      {/* Oversized, barely-there version of the same artwork as the tile. It fills the empty
          right-hand side that the reference card leaves to whitespace. */}
      <span
        aria-hidden="true"
        className="pointer-events-none absolute -top-6 -right-6 block size-52 text-foreground/5 dark:text-white/5"
      >
        <ItemIllustration itemType={report.itemTypeName} category={report.categoryName} />
      </span>

      <div className="relative flex flex-col gap-6">
        {/* ------------------------------------------------------------ heading */}
        <div className="flex items-start gap-3">
          <span
            className={cn(
              'flex size-11 shrink-0 items-center justify-center overflow-hidden rounded-2xl bg-foreground text-background',
              isWithdrawn && 'bg-muted text-muted-foreground',
            )}
          >
            <ItemMedia
              photoUrl={report.photoUrls?.[0]}
              itemType={report.itemTypeName}
              category={report.categoryName}
              illustrationClassName="size-6"
            />
          </span>

          <div className="min-w-0 flex-1">
            <h2 className="truncate font-medium">{report.itemTypeName}</h2>
            <p className="truncate text-sm text-muted-foreground">
              {report.lastSeenLocationName} · {report.description}
            </p>
          </div>

          {report.status === 'Active' && (
            <Button
              variant="outline"
              size="sm"
              onClick={onWithdraw}
              disabled={isWithdrawing}
              className="-mt-1 shrink-0 border-foreground/15 bg-background/60 hover:bg-background"
            >
              {isWithdrawing && <Loader2Icon className="animate-spin" aria-hidden="true" />}
              Withdraw
            </Button>
          )}
        </div>

        {/* -------------------------------------------------------- found notice */}
        {!isWithdrawn && report.foundClaimCount > 0 && (
          <p className="fu-reveal flex items-start gap-2.5 rounded-xl border border-brand-green/35 bg-brand-green/10 p-3 text-sm">
            <BellRingIcon
              className="mt-0.5 size-4 shrink-0 text-brand-forest dark:text-brand-sage"
              aria-hidden="true"
            />
            <span>
              <span className="font-medium">
                {report.foundClaimCount === 1
                  ? 'Someone says they found this'
                  : `${report.foundClaimCount} people say they found this`}
              </span>
              {report.lastFoundClaimAt && (
                <span className="text-muted-foreground"> · {timeAgo(report.lastFoundClaimAt)}</span>
              )}
              {report.messageCount > 0 && (
                <span className="text-muted-foreground">
                  {' '}
                  · {report.messageCount} message{report.messageCount === 1 ? '' : 's'}
                </span>
              )}
            </span>
          </p>
        )}

        {/* -------------------------------------------------------------- metric */}
        {isWithdrawn ? (
          <p className="text-3xl font-semibold tracking-tight">
            Withdrawn
            <span className="pl-2 text-lg font-normal text-muted-foreground">
              and off the feed
            </span>
          </p>
        ) : (
          <p className="text-4xl font-semibold tracking-tight tabular-nums">
            {value}
            <span className="pl-2 text-lg font-normal text-muted-foreground">
              {unit} on the feed
            </span>
          </p>
        )}

        {/* --------------------------------------------------------------- track */}
        <div className="flex flex-col gap-3">
          <div className={cn('relative h-2', !isWithdrawn && 'mt-8')}>
            <div className="absolute inset-0 rounded-full bg-foreground/10" />

            {!isWithdrawn && (
              <>
                <div
                  className="absolute inset-y-0 left-0 rounded-full bg-brand-green transition-[width] duration-700 ease-out"
                  style={{ width: STAGE_OFFSET[stage] }}
                />

                {/* Every checkpoint, so the track shows how far along the marker actually is
                    rather than only where it stopped. */}
                {STAGE_OFFSET.map((offset, index) => (
                  <span
                    key={offset}
                    aria-hidden="true"
                    className={cn(
                      'absolute top-1/2 size-1.5 -translate-x-1/2 -translate-y-1/2 rounded-full transition-colors duration-700',
                      index < stage ? 'bg-brand-forest/50' : 'bg-foreground/20',
                      index === stage && 'opacity-0',
                    )}
                    style={{ left: offset }}
                  />
                ))}

                {/* The pill sits in its own full-width layer rather than inside the marker:
                    centred on the knob it hangs off the card at the first and last stages,
                    which clips on a narrow screen. At the ends it hangs from that edge. */}
                <span
                  className={cn(
                    'absolute bottom-4 flex max-w-full items-center gap-1.5 rounded-full bg-foreground px-2.5 py-1 text-xs whitespace-nowrap text-background transition-[left] duration-700 ease-out',
                    PILL_ALIGN[stage],
                  )}
                  style={
                    PILL_ALIGN[stage] === '-translate-x-1/2'
                      ? { left: STAGE_OFFSET[stage] }
                      : undefined
                  }
                >
                  <span className="opacity-60">now</span>
                  <span className="truncate font-medium">{LIFECYCLE[stage]}</span>
                </span>

                <span
                  className="absolute top-1/2 flex size-5 -translate-x-1/2 -translate-y-1/2 transition-[left] duration-700 ease-out"
                  style={{ left: STAGE_OFFSET[stage] }}
                >
                  <span className="fu-ping absolute inline-flex size-full rounded-full bg-brand-green/60" />
                  <span className="relative inline-flex size-5 rounded-full border-2 border-foreground bg-background" />
                </span>
              </>
            )}
          </div>

          <div className="flex items-start justify-between gap-4 text-sm">
            <div>
              <p className="font-medium">{shortDate(report.createdAt)}</p>
              <p className="text-xs text-muted-foreground">
                {isWithdrawn ? 'Reported' : LIFECYCLE[0]}
              </p>
            </div>

            <div className="text-right">
              <p className={cn('font-medium', isWithdrawn && 'text-muted-foreground')}>
                {isWithdrawn ? 'Off the feed' : LIFECYCLE[3]}
              </p>
              <p className="text-xs text-muted-foreground">
                {isWithdrawn ? 'Not being matched' : 'Next goal'}
              </p>
            </div>
          </div>
        </div>
      </div>
    </article>
  )
}
