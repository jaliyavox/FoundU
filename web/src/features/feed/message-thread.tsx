import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2Icon, SendIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import { getMessages, sendMessage, timeAgo, type LostReportMessage } from './feed-api'

/**
 * One conversation on a lost report, laid out as a chat: the reader's messages on the right.
 *
 * Used on both sides. A finder sees their own thread with the author. The author sees every
 * thread and picks which finder they are replying to - the API refuses a reply to anyone who
 * has not written first, so the author can never open contact with a stranger.
 *
 * Nothing here carries contact details. The thread is the only channel, and it is in-app.
 */
export function MessageThread({
  reportId,
  isAuthor,
  tone = 'light',
  className,
}: {
  reportId: string
  /** The author replies into threads; anyone else writes to the author. */
  isAuthor: boolean
  tone?: 'light' | 'dark'
  className?: string
}) {
  const queryClient = useQueryClient()
  const [body, setBody] = useState('')
  const [replyTo, setReplyTo] = useState<string | null>(null)

  const { data, isPending, isError, error } = useQuery({
    queryKey: ['report-messages', reportId],
    queryFn: () => getMessages(reportId),
    // A finder who has not written yet gets 403; that is an empty thread, not a failure.
    retry: false,
  })

  const messages: LostReportMessage[] = isError && error instanceof ApiError && error.status === 403 ? [] : (data ?? [])

  // Threads, keyed by the finder. A finder only ever has one.
  const threads = new Map<string, { name: string; messages: LostReportMessage[] }>()
  for (const message of messages) {
    const thread = threads.get(message.counterpartId) ?? { name: message.counterpartName, messages: [] }
    thread.messages.push(message)
    threads.set(message.counterpartId, thread)
  }
  const activeThread = isAuthor ? (replyTo ?? threads.keys().next().value ?? null) : (threads.keys().next().value ?? null)

  const send = useMutation({
    mutationFn: () => sendMessage(reportId, body.trim(), isAuthor ? (activeThread ?? undefined) : undefined),
    onSuccess: () => {
      setBody('')
      queryClient.invalidateQueries({ queryKey: ['report-messages', reportId] })
    },
    onError: (err) => toast.error(err instanceof ApiError ? err.message : 'Could not reach the server.'),
  })

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (body.trim().length > 0) send.mutate()
  }

  const dark = tone === 'dark'
  const muted = dark ? 'text-white/55' : 'text-neutral-500'

  if (isPending) return <p className={cn('text-sm', muted)}>Loading messages…</p>

  if (isError && !(error instanceof ApiError && error.status === 403)) {
    return <p className={cn('text-sm', muted)}>Could not load messages.</p>
  }

  const shown = activeThread ? (threads.get(activeThread)?.messages ?? []) : []

  return (
    <div className={cn('flex flex-col gap-3', className)}>
      {isAuthor && threads.size === 0 && (
        <p className={cn('text-sm', muted)}>Nobody has written about this yet. When a finder does, you can reply here.</p>
      )}

      {isAuthor && threads.size > 1 && (
        <div className="flex flex-wrap gap-2" role="tablist" aria-label="Conversations">
          {[...threads.entries()].map(([id, thread]) => (
            <button
              key={id}
              type="button"
              role="tab"
              aria-selected={id === activeThread}
              onClick={() => setReplyTo(id)}
              className={cn(
                'rounded-full px-3 py-1 text-xs font-medium transition-colors',
                id === activeThread
                  ? dark ? 'bg-white text-brand-forest' : 'bg-neutral-900 text-white'
                  : dark ? 'bg-white/10 text-white/70 hover:bg-white/15' : 'bg-neutral-900/6 text-neutral-700 hover:bg-neutral-900/10',
              )}
            >
              {thread.name}
            </button>
          ))}
        </div>
      )}

      {shown.length > 0 && (
        <ol className="flex flex-col gap-2">
          {shown.map((message) => (
            <li key={message.id} className={cn('flex flex-col gap-0.5', message.isMine ? 'items-end' : 'items-start')}>
              <div
                className={cn(
                  'max-w-[85%] rounded-2xl px-3.5 py-2 text-sm leading-relaxed text-pretty',
                  message.isMine
                    ? 'rounded-br-md bg-brand-forest text-white'
                    : dark ? 'rounded-bl-md bg-white/10 text-white' : 'rounded-bl-md bg-neutral-900/6 text-neutral-900',
                )}
              >
                {message.body}
              </div>
              <span className={cn('px-1 text-[11px]', muted)}>
                {message.isMine ? 'You' : message.senderName.split(' ')[0]} · {timeAgo(message.createdAt)}
              </span>
            </li>
          ))}
        </ol>
      )}

      {/* The author only gets a box once a thread exists; a finder always has one. */}
      {(!isAuthor || activeThread) && (
        <form onSubmit={handleSubmit} className="flex flex-col gap-2">
          <Textarea
            rows={2}
            required
            value={body}
            onChange={(event) => setBody(event.target.value)}
            placeholder={
              isAuthor
                ? `Reply to ${threads.get(activeThread!)?.name.split(' ')[0] ?? 'them'}`
                : 'I found this and handed it in at the library desk this morning.'
            }
            aria-label={isAuthor ? 'Your reply' : 'Your message'}
            className={cn(dark && 'border-white/15 bg-white/[0.06] text-white placeholder:text-white/35')}
          />
          <Button
            type="submit"
            size="sm"
            disabled={send.isPending || body.trim().length === 0}
            className="self-end bg-brand-forest text-white hover:bg-brand-forest/90"
          >
            {send.isPending ? <Loader2Icon className="animate-spin" aria-hidden="true" /> : <SendIcon aria-hidden="true" />}
            {isAuthor ? 'Reply' : 'Send'}
          </Button>
        </form>
      )}
    </div>
  )
}
