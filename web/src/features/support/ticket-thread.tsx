import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Loader2Icon, SendIcon, ShieldCheckIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import { timeAgo } from '@/features/feed/feed-api'
import { replyToTicket, type TicketDetail } from './support-api'

/**
 * One support conversation, used by both sides - the person who raised it and whoever is on
 * the desk. There is no second copy of the thread: the same ticket, different powers.
 *
 * A closed ticket shows its history and no box, because the API will refuse a reply and a
 * disabled-looking form that fails on submit is worse than none.
 */
export function TicketThread({ ticket }: { ticket: TicketDetail }) {
  const queryClient = useQueryClient()
  const [body, setBody] = useState('')

  const reply = useMutation({
    mutationFn: () => replyToTicket(ticket.id, body.trim()),
    onSuccess: updated => {
      setBody('')
      queryClient.setQueryData(['support-ticket', ticket.id], updated)
      queryClient.invalidateQueries({ queryKey: ['my-tickets'] })
      queryClient.invalidateQueries({ queryKey: ['support-queue'] })
      queryClient.invalidateQueries({ queryKey: ['support-stats'] })
    },
    onError: error =>
      toast.error(error instanceof ApiError ? error.message : 'Could not send that.'),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    if (body.trim() && !reply.isPending) reply.mutate()
  }

  return (
    <div className="flex flex-col gap-4">
      <ol className="flex flex-col gap-3">
        {ticket.messages.map(message => (
          <li
            key={message.id}
            className={cn('flex flex-col gap-1', message.isMine ? 'items-end' : 'items-start')}
          >
            <div
              className={cn(
                'max-w-[85%] rounded-2xl px-4 py-2.5 text-sm leading-relaxed text-pretty',
                message.isMine
                  ? 'rounded-br-md bg-brand-forest text-white'
                  : 'rounded-bl-md bg-muted text-foreground',
              )}
            >
              {message.body}
            </div>
            <span className="flex items-center gap-1.5 px-1 text-[11px] text-muted-foreground">
              {message.isStaffReply && !message.isMine && (
                <ShieldCheckIcon className="size-3" aria-hidden="true" />
              )}
              {message.isMine ? 'You' : message.senderName.split(' ')[0]}
              {message.isStaffReply && ' · FoundU'} · {timeAgo(message.createdAt)}
            </span>
          </li>
        ))}
      </ol>

      {ticket.status === 'Closed' ? (
        <p className="rounded-xl border border-foreground/8 bg-muted/40 p-3 text-sm text-muted-foreground">
          This ticket is closed. Open a new one if you need anything else.
        </p>
      ) : (
        <form onSubmit={submit} className="flex flex-col gap-2">
          <Textarea
            rows={3}
            required
            value={body}
            onChange={event => setBody(event.target.value)}
            maxLength={4000}
            placeholder="Add to this ticket…"
            aria-label="Your message"
          />
          <Button type="submit" size="sm" className="self-end" disabled={reply.isPending || !body.trim()}>
            {reply.isPending ? (
              <Loader2Icon className="animate-spin" aria-hidden="true" />
            ) : (
              <SendIcon aria-hidden="true" />
            )}
            Send
          </Button>
        </form>
      )}
    </div>
  )
}
