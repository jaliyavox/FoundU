import { useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeftIcon, ClockIcon, InboxIcon, Loader2Icon, UserRoundIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider } from '@/components/layout/dashboard-panel'
import { Skeleton } from '@/components/ui/skeleton'
import { FormSelect } from '@/features/reports/form-select'
import { timeAgo } from '@/features/feed/feed-api'
import { useAuth } from '@/features/auth/use-auth'
import { ApiError } from '@/lib/api/client'
import { TicketThread } from './ticket-thread'
import {
  CATEGORY_LABELS,
  STATUS_LABELS,
  getSupportQueue,
  getSupportStats,
  getTicket,
  updateTicket,
  type TicketStatus,
} from './support-api'

const STATUS_FILTERS = [
  { value: 'all', label: 'Everything' },
  { value: 'Open', label: 'With us' },
  { value: 'Waiting', label: 'Waiting on them' },
  { value: 'Resolved', label: 'Resolved' },
  { value: 'Closed', label: 'Closed' },
]

/**
 * The support queue, for the people who answer it.
 *
 * Ordered by who has waited longest rather than newest first: a queue sorted by arrival
 * quietly buries the person nobody got to.
 */
export function AdminSupportPage() {
  const [openId, setOpenId] = useState<string | null>(null)
  const [status, setStatus] = useState('Open')
  const [assigned, setAssigned] = useState('all')

  const stats = useQuery({ queryKey: ['support-stats'], queryFn: getSupportStats })

  const queue = useQuery({
    queryKey: ['support-queue', { status, assigned }],
    queryFn: () =>
      getSupportQueue({
        pageSize: 50,
        status: status === 'all' ? undefined : status,
        assigned: assigned === 'all' ? undefined : (assigned as 'mine' | 'none'),
      }),
    placeholderData: keepPreviousData,
  })

  if (openId) return <QueueTicket id={openId} onBack={() => setOpenId(null)} />

  return (
    <section className="flex w-full flex-col gap-6">
      <div>
        <p className="text-sm font-medium text-brand-green">Support</p>
        <h1 className="pt-1 text-2xl font-semibold tracking-tight">The desk queue</h1>
        <p className="max-w-xl pt-2 text-sm text-muted-foreground">
          Longest wait first. Answering a ticket nobody has picked up assigns it to you.
        </p>
      </div>

      <DashboardPanel className="grid gap-6 sm:grid-cols-4">
        {stats.isPending ? (
          <Skeleton className="h-16 w-full sm:col-span-4" />
        ) : stats.isError ? (
          <p className="text-sm text-muted-foreground sm:col-span-4">Could not load the queue figures.</p>
        ) : (
          <>
            <Figure value={stats.data.open} label="with us" emphasis />
            <Figure value={stats.data.unassigned} label="nobody has picked up" />
            <Figure value={stats.data.waiting} label="waiting on them" />
            <Figure value={stats.data.oldestOpenHours} label="hours, longest wait" />
          </>
        )}
      </DashboardPanel>

      <DashboardPanel className="flex flex-col gap-4">
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="flex flex-col gap-2">
            <label htmlFor="queue-status" className="text-sm font-medium">Status</label>
            <FormSelect
              id="queue-status"
              value={status}
              onValueChange={setStatus}
              options={STATUS_FILTERS}
              placeholder="Everything"
            />
          </div>
          <div className="flex flex-col gap-2">
            <label htmlFor="queue-assigned" className="text-sm font-medium">Assigned</label>
            <FormSelect
              id="queue-assigned"
              value={assigned}
              onValueChange={setAssigned}
              options={[
                { value: 'all', label: 'Anyone' },
                { value: 'mine', label: 'Mine' },
                { value: 'none', label: 'Nobody yet' },
              ]}
              placeholder="Anyone"
            />
          </div>
        </div>

        <PanelDivider />

        {queue.isPending ? (
          <Skeleton className="h-40 w-full" />
        ) : queue.isError ? (
          <div role="alert" className="flex flex-col items-start gap-2">
            <p className="text-sm">Could not load the queue.</p>
            <Button variant="outline" size="sm" onClick={() => queue.refetch()}>Try again</Button>
          </div>
        ) : queue.data.items.length === 0 ? (
          <p className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
            <InboxIcon className="size-4" aria-hidden="true" />
            Nothing here. That is the good outcome.
          </p>
        ) : (
          <ol className="flex flex-col">
            {queue.data.items.map((ticket, index) => (
              <li key={ticket.id} className={index === 0 ? '' : 'border-t border-foreground/8'}>
                <button
                  type="button"
                  onClick={() => setOpenId(ticket.id)}
                  className="flex w-full flex-col gap-1 py-4 text-left transition-colors hover:bg-foreground/[0.03] focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
                >
                  <div className="flex flex-wrap items-baseline justify-between gap-2">
                    <span className="font-medium">
                      {ticket.unreadCount > 0 && (
                        <span className="mr-2 rounded-full bg-brand-forest px-2 py-0.5 text-[11px] font-medium text-white">
                          {ticket.unreadCount}
                        </span>
                      )}
                      {ticket.subject}
                    </span>
                    <span className="flex items-center gap-1 text-xs text-muted-foreground">
                      <ClockIcon className="size-3" aria-hidden="true" />
                      {timeAgo(ticket.lastActivityAt)}
                    </span>
                  </div>
                  <p className="text-sm text-muted-foreground">
                    {ticket.raisedByName} · {CATEGORY_LABELS[ticket.category]} · {STATUS_LABELS[ticket.status]}
                    {ticket.assignedToName
                      ? ` · ${ticket.assignedToName.split(' ')[0]}`
                      : ' · nobody yet'}
                  </p>
                </button>
              </li>
            ))}
          </ol>
        )}
      </DashboardPanel>
    </section>
  )
}

function Figure({ value, label, emphasis }: { value: number; label: string; emphasis?: boolean }) {
  return (
    <div className="flex flex-col gap-1">
      <p className={emphasis ? 'text-4xl font-semibold tracking-tight tabular-nums' : 'text-3xl font-semibold tracking-tight tabular-nums'}>
        {value}
      </p>
      <p className="text-sm text-muted-foreground">{label}</p>
    </div>
  )
}

function QueueTicket({ id, onBack }: { id: string; onBack: () => void }) {
  const queryClient = useQueryClient()
  const { user } = useAuth()

  const { data, isPending, isError, refetch } = useQuery({
    queryKey: ['support-ticket', id],
    queryFn: () => getTicket(id),
  })

  const update = useMutation({
    mutationFn: (input: { status: TicketStatus; assignedToUserId?: string | null }) => updateTicket(id, input),
    onSuccess: updated => {
      queryClient.setQueryData(['support-ticket', id], updated)
      queryClient.invalidateQueries({ queryKey: ['support-queue'] })
      queryClient.invalidateQueries({ queryKey: ['support-stats'] })
      toast.success('Ticket updated.')
    },
    onError: error => toast.error(error instanceof ApiError ? error.message : 'Could not update the ticket.'),
  })

  return (
    <section className="flex w-full flex-col gap-6">
      <Button variant="ghost" size="sm" className="self-start" onClick={onBack}>
        <ArrowLeftIcon aria-hidden="true" />
        Back to the queue
      </Button>

      {isPending ? (
        <Skeleton className="h-64 w-full" />
      ) : isError ? (
        <DashboardPanel className="flex flex-col items-start gap-3" role="alert">
          <p className="text-sm">Could not load this ticket.</p>
          <Button variant="outline" size="sm" onClick={() => refetch()}>Try again</Button>
        </DashboardPanel>
      ) : (
        <>
          <DashboardPanel className="flex flex-col gap-4">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h1 className="text-xl font-semibold tracking-tight">{data.subject}</h1>
                <p className="pt-1 text-sm text-muted-foreground">
                  {CATEGORY_LABELS[data.category]} · opened {timeAgo(data.createdAt)} by {data.raisedByName}
                  {data.raisedByEmail && ` · ${data.raisedByEmail}`}
                </p>
              </div>
              <span className="flex items-center gap-1.5 text-sm text-muted-foreground">
                <UserRoundIcon className="size-4" aria-hidden="true" />
                {data.assignedToName ?? 'Nobody yet'}
              </span>
            </div>

            <PanelDivider />

            <div className="flex flex-wrap items-center gap-2">
              {(['Open', 'Waiting', 'Resolved', 'Closed'] as TicketStatus[]).map(status => (
                <Button
                  key={status}
                  size="sm"
                  variant={data.status === status ? 'default' : 'outline'}
                  disabled={update.isPending || data.status === status}
                  onClick={() => update.mutate({ status, assignedToUserId: data.assignedToUserId })}
                >
                  {update.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
                  {STATUS_LABELS[status]}
                </Button>
              ))}

              {data.assignedToUserId !== user?.id && (
                <Button
                  size="sm"
                  variant="ghost"
                  disabled={update.isPending}
                  onClick={() => update.mutate({ status: data.status, assignedToUserId: user?.id ?? null })}
                >
                  Assign to me
                </Button>
              )}
            </div>
          </DashboardPanel>

          <DashboardPanel>
            <TicketThread ticket={data} />
          </DashboardPanel>
        </>
      )}
    </section>
  )
}
