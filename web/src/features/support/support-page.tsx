import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeftIcon, LifeBuoyIcon, Loader2Icon, PlusIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider } from '@/components/layout/dashboard-panel'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { FormSelect } from '@/features/reports/form-select'
import { timeAgo } from '@/features/feed/feed-api'
import { ApiError } from '@/lib/api/client'
import { AssistantBadge } from './assistant-badge'
import { SupportAssistant } from './support-assistant'
import { TicketThread } from './ticket-thread'
import {
  CATEGORY_LABELS,
  STATUS_LABELS,
  TICKET_CATEGORIES,
  createTicket,
  getMyTickets,
  getTicket,
  type TicketCategory,
} from './support-api'

/**
 * Help from the people who run FoundU, for anyone signed in.
 *
 * Separate from the item threads: those are two students talking about one thing, and they
 * end when the item does. A ticket outlives the item it was about.
 */
export function SupportPage() {
  const [openId, setOpenId] = useState<string | null>(null)
  const [composing, setComposing] = useState(false)

  if (openId) return <TicketView id={openId} onBack={() => setOpenId(null)} />

  return (
    <section className="mx-auto flex w-full max-w-2xl flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-brand-green">Support</p>
          <h1 className="pt-1 text-2xl font-semibold tracking-tight">Ask us for help</h1>
          <p className="max-w-xl pt-2 text-sm text-muted-foreground">
            Something stuck, a code that will not work, a claim that went the wrong way - ask the
            assistant first. If it cannot sort it, it writes the ticket for you.
          </p>
        </div>
        {!composing && (
          <Button onClick={() => setComposing(true)}>
            <PlusIcon aria-hidden="true" />
            Write a ticket myself
          </Button>
        )}
      </div>

      {composing
        ? <NewTicketForm onDone={id => { setComposing(false); setOpenId(id) }} onCancel={() => setComposing(false)} />
        : <SupportAssistant onTicketOpened={setOpenId} />}

      <TicketList onOpen={setOpenId} />
    </section>
  )
}

function NewTicketForm({ onDone, onCancel }: { onDone: (id: string) => void; onCancel: () => void }) {
  const queryClient = useQueryClient()
  const [subject, setSubject] = useState('')
  const [category, setCategory] = useState<TicketCategory>('Other')
  const [body, setBody] = useState('')
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  const create = useMutation({
    mutationFn: () => createTicket({ subject: subject.trim(), category, body: body.trim() }),
    onSuccess: ticket => {
      queryClient.invalidateQueries({ queryKey: ['my-tickets'] })
      toast.success('Opened. We will answer on the ticket.')
      onDone(ticket.id)
    },
    onError: error => {
      if (error instanceof ApiError) {
        setFieldErrors(error.fieldErrors ?? {})
        toast.error(error.message)
      } else {
        toast.error('Could not open the ticket.')
      }
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    if (!create.isPending) create.mutate()
  }

  return (
    <DashboardPanel>
      <form onSubmit={submit} className="flex flex-col gap-5">
        <div className="flex items-center gap-2">
          <LifeBuoyIcon className="size-4 text-muted-foreground" aria-hidden="true" />
          <h2 className="font-heading text-base font-medium">What do you need?</h2>
        </div>

        <PanelDivider />

        <div className="flex flex-col gap-2">
          <Label htmlFor="ticket-subject">Subject</Label>
          <Input
            id="ticket-subject"
            value={subject}
            onChange={event => setSubject(event.target.value)}
            maxLength={200}
            required
            placeholder="My collection code will not work"
          />
          {fieldErrors.Subject?.map(message => (
            <p key={message} role="alert" className="text-sm text-destructive">{message}</p>
          ))}
        </div>

        <div className="flex flex-col gap-2">
          <Label htmlFor="ticket-category">What is it about?</Label>
          <FormSelect
            id="ticket-category"
            value={category}
            onValueChange={value => setCategory(value as TicketCategory)}
            options={TICKET_CATEGORIES.map(value => ({ value, label: CATEGORY_LABELS[value] }))}
            placeholder="Choose one"
          />
        </div>

        <div className="flex flex-col gap-2">
          <Label htmlFor="ticket-body">What happened?</Label>
          <Textarea
            id="ticket-body"
            value={body}
            onChange={event => setBody(event.target.value)}
            rows={4}
            maxLength={4000}
            required
            placeholder="I was approved for my backpack on Tuesday, but the desk says the code has already been used."
          />
          <p className="text-xs text-muted-foreground">
            Leave out passwords. Never send the answer to a verification question here.
          </p>
          {fieldErrors.Body?.map(message => (
            <p key={message} role="alert" className="text-sm text-destructive">{message}</p>
          ))}
        </div>

        <div className="flex items-center gap-2">
          <Button type="submit" disabled={create.isPending}>
            {create.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
            Open ticket
          </Button>
          <Button type="button" variant="ghost" onClick={onCancel} disabled={create.isPending}>
            Cancel
          </Button>
        </div>
      </form>
    </DashboardPanel>
  )
}

function TicketList({ onOpen }: { onOpen: (id: string) => void }) {
  const { data, isPending, isError, refetch } = useQuery({
    queryKey: ['my-tickets'],
    queryFn: () => getMyTickets({ pageSize: 50 }),
  })

  if (isPending) return <Skeleton className="h-40 w-full" />

  if (isError) {
    return (
      <DashboardPanel className="flex flex-col items-start gap-3" role="alert">
        <p className="text-sm">Could not load your tickets.</p>
        <Button variant="outline" size="sm" onClick={() => refetch()}>Try again</Button>
      </DashboardPanel>
    )
  }

  if (data.items.length === 0) {
    return (
      <DashboardPanel>
        <p className="text-sm text-muted-foreground">
          You have not asked us anything yet. When you do, the conversation lives here.
        </p>
      </DashboardPanel>
    )
  }

  return (
    <DashboardPanel className="flex flex-col gap-4">
      <h2 className="font-heading text-base font-medium">Your tickets</h2>
      <PanelDivider />
      <ol className="flex flex-col">
        {data.items.map((ticket, index) => (
          <li key={ticket.id} className={index === 0 ? '' : 'border-t border-foreground/8'}>
            <button
              type="button"
              onClick={() => onOpen(ticket.id)}
              className="flex w-full flex-col gap-1 py-4 text-left transition-colors hover:bg-foreground/[0.03] focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
            >
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <span className="font-medium">{ticket.subject}</span>
                <span className="text-xs text-muted-foreground">
                  {ticket.unreadCount > 0 && (
                    <span className="mr-2 rounded-full bg-brand-forest px-2 py-0.5 text-[11px] font-medium text-white">
                      {ticket.unreadCount} new
                    </span>
                  )}
                  {timeAgo(ticket.lastActivityAt)}
                </span>
              </div>
              <p className="text-sm text-muted-foreground">
                {CATEGORY_LABELS[ticket.category]} · {STATUS_LABELS[ticket.status]}
                {ticket.assignedToName && ` · with ${ticket.assignedToName.split(' ')[0]}`}
              </p>
            </button>
          </li>
        ))}
      </ol>
    </DashboardPanel>
  )
}

function TicketView({ id, onBack }: { id: string; onBack: () => void }) {
  const { data, isPending, isError, refetch } = useQuery({
    queryKey: ['support-ticket', id],
    queryFn: () => getTicket(id),
  })

  return (
    <section className="mx-auto flex w-full max-w-2xl flex-col gap-6">
      <Button variant="ghost" size="sm" className="self-start" onClick={onBack}>
        <ArrowLeftIcon aria-hidden="true" />
        All tickets
      </Button>

      {isPending ? (
        <Skeleton className="h-64 w-full" />
      ) : isError ? (
        <DashboardPanel className="flex flex-col items-start gap-3" role="alert">
          <p className="text-sm">Could not load this ticket.</p>
          <Button variant="outline" size="sm" onClick={() => refetch()}>Try again</Button>
        </DashboardPanel>
      ) : (
        <DashboardPanel className="flex flex-col gap-4">
          <div>
            <h1 className="text-xl font-semibold tracking-tight">{data.subject}</h1>
            <p className="pt-1 text-sm text-muted-foreground">
              {CATEGORY_LABELS[data.category]} · {STATUS_LABELS[data.status]} · opened {timeAgo(data.createdAt)}
            </p>
            {data.viaAssistant && <div className="pt-2"><AssistantBadge /></div>}
          </div>
          <PanelDivider />
          <TicketThread ticket={data} />
        </DashboardPanel>
      )}
    </section>
  )
}
