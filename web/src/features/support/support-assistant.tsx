import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { BotIcon, CheckIcon, Loader2Icon, SendIcon, SparklesIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider } from '@/components/layout/dashboard-panel'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { FormSelect } from '@/features/reports/form-select'
import { ApiError } from '@/lib/api/client'
import {
  CATEGORY_LABELS,
  TICKET_CATEGORIES,
  askSupportAssistant,
  createTicket,
  type AssistantResponse,
  type AssistantTurn,
  type TicketCategory,
  type TicketDraft,
} from './support-api'

const GREETING = 'Hi - what is going wrong? Tell me in a sentence or two, and I will either sort it or pass it to the desk.'

/**
 * Ask before opening a ticket. Answers come from FoundU's help guide, about the person's own
 * claims and reports where that helps. When it cannot help it drafts a ticket - and the
 * person reads it and sends it; nothing is sent for them.
 */
export function SupportAssistant({ onTicketOpened }: { onTicketOpened: (id: string) => void }) {
  const [turns, setTurns] = useState<AssistantTurn[]>([{ role: 'assistant', text: GREETING }])
  const [last, setLast] = useState<AssistantResponse | null>(null)
  const [input, setInput] = useState('')
  const [solved, setSolved] = useState(false)

  const ask = useMutation({
    mutationFn: ({ message, history }: { message: string; history: AssistantTurn[] }) =>
      askSupportAssistant(message, history, last?.topic ?? null),
    onSuccess: response => {
      setLast(response)
      setTurns(previous => [...previous, { role: 'assistant', text: response.reply }])
      setInput('')
    },
  })

  function send(message: string) {
    const text = message.trim()
    if (!text || ask.isPending) return
    setSolved(false)
    // What was said before this message. The greeting is the page's, not the agent's, so it
    // is left out; the API adds this message itself.
    const history = turns.slice(1)
    setTurns(previous => [...previous, { role: 'user', text }])
    ask.mutate({ message: text, history })
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    send(input)
  }

  function restart() {
    ask.reset()
    setTurns([{ role: 'assistant', text: GREETING }])
    setLast(null)
    setSolved(false)
    setInput('')
  }

  const draft = last && !ask.isPending && last.ticket ? last.ticket : null

  return (
    <DashboardPanel className="flex flex-col gap-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex items-center gap-2">
          <SparklesIcon className="size-4 text-brand-green" aria-hidden="true" />
          <h2 className="font-heading text-base font-medium">Ask the assistant first</h2>
        </div>
        {turns.length > 1 && (
          <Button variant="ghost" size="sm" onClick={restart} disabled={ask.isPending}>Start again</Button>
        )}
      </div>

      <ol role="log" aria-label="Conversation with the support assistant" aria-live="polite" className="flex flex-col gap-4">
        {turns.map((turn, index) => (
          <li key={index} className={turn.role === 'user' ? 'ml-8 rounded-xl bg-muted p-3' : 'flex gap-3'}>
            {turn.role === 'assistant' && <BotIcon className="mt-1 size-4 shrink-0 text-brand-green" aria-hidden="true" />}
            <div>
              <p className="text-xs font-medium text-muted-foreground">{turn.role === 'user' ? 'You' : 'FoundU assistant'}</p>
              <p className="pt-1 text-sm leading-relaxed whitespace-pre-wrap">{turn.text}</p>
            </div>
          </li>
        ))}
      </ol>

      {ask.isPending && (
        <p role="status" className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2Icon className="size-4 animate-spin" aria-hidden="true" />
          Looking into it…
        </p>
      )}
      {ask.isError && (
        <p role="alert" className="text-sm text-destructive">
          {ask.error instanceof ApiError ? ask.error.message : 'Could not reach the assistant.'} Send it again, or write a ticket yourself.
        </p>
      )}

      {last?.phase === 'answered' && !ask.isPending && !solved && (
        <div className="flex flex-wrap gap-2 pl-7">
          <Button variant="outline" size="sm" onClick={() => setSolved(true)}>
            <CheckIcon aria-hidden="true" />
            Yes, that sorted it
          </Button>
          <Button variant="outline" size="sm" onClick={() => send("That didn't solve it - I still need help.")}>
            No, I still need help
          </Button>
        </div>
      )}
      {solved && <p role="status" className="pl-7 text-sm text-brand-green">Glad that is sorted. Ask again any time.</p>}

      {draft && <DraftTicket key={JSON.stringify(draft)} draft={draft} onSent={onTicketOpened} />}

      <PanelDivider />
      <form onSubmit={submit} className="flex flex-col gap-3">
        <Label htmlFor="assistant-message" className="sr-only">Your question</Label>
        <Textarea
          id="assistant-message"
          value={input}
          onChange={event => setInput(event.target.value)}
          maxLength={1000}
          rows={2}
          disabled={ask.isPending}
          placeholder="My collection code is not accepted at the desk."
        />
        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="text-xs text-muted-foreground">Leave out passwords and the answers to verification questions.</p>
          <Button type="submit" disabled={!input.trim() || ask.isPending}>
            <SendIcon aria-hidden="true" />
            Ask
          </Button>
        </div>
      </form>
    </DashboardPanel>
  )
}

/** The ticket the assistant prepared. Every field can be changed before it goes. */
function DraftTicket({ draft, onSent }: { draft: TicketDraft; onSent: (id: string) => void }) {
  const queryClient = useQueryClient()
  const [subject, setSubject] = useState(draft.subject)
  const [category, setCategory] = useState<TicketCategory>(draft.category)
  const [body, setBody] = useState(draft.body)

  const send = useMutation({
    mutationFn: () => createTicket({ subject: subject.trim(), category, body: body.trim(), viaAssistant: true }),
    onSuccess: ticket => {
      queryClient.invalidateQueries({ queryKey: ['my-tickets'] })
      toast.success('Sent to the support team. They will answer on the ticket.')
      onSent(ticket.id)
    },
    onError: error => toast.error(error instanceof ApiError ? error.message : 'Could not send the ticket.'),
  })

  return (
    <div className="flex flex-col gap-4 rounded-xl border border-brand-forest/20 bg-brand-forest/[0.03] p-4 dark:border-brand-green/25 dark:bg-brand-green/[0.06]">
      <div>
        <p className="text-sm font-medium">A ticket for the support team</p>
        <p className="pt-1 text-xs text-muted-foreground">I wrote this from what you told me. Check it, change anything, then send it.</p>
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor="draft-subject">Subject</Label>
        <Input id="draft-subject" value={subject} onChange={event => setSubject(event.target.value)} maxLength={200} />
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor="draft-category">What is it about?</Label>
        <FormSelect
          id="draft-category"
          value={category}
          onValueChange={value => setCategory(value as TicketCategory)}
          options={TICKET_CATEGORIES.map(value => ({ value, label: CATEGORY_LABELS[value] }))}
          placeholder="Choose one"
        />
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor="draft-body">Message</Label>
        <Textarea id="draft-body" value={body} onChange={event => setBody(event.target.value)} rows={6} maxLength={4000} />
      </div>
      <Button
        className="self-start"
        disabled={send.isPending || !subject.trim() || body.trim().length < 10}
        onClick={() => send.mutate()}
      >
        {send.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
        Send to the support team
      </Button>
    </div>
  )
}
