import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRightIcon, BotIcon, HandIcon, Loader2Icon, MapPinIcon, SearchIcon, SendIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider } from '@/components/layout/dashboard-panel'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/use-auth'
import { FormSelect } from '@/features/reports/form-select'
import { getMyLostReports } from '@/features/reports/reports-api'
import { createClaim } from '@/features/claims/claims-api'
import { recogniseFoundPost } from '@/features/feed/feed-api'
import { ApiError } from '@/lib/api/client'
import { askIntake, isFinder, readIntakeHandoff, slotsFor, type IntakeIntent, type IntakeResponse } from './intake-api'

const GREETING = 'Lost something, or found something? Tell me what it is, its colour, and where.'
const GREETINGS: Record<IntakeIntent, string> = {
  lost: 'What did you lose? Tell me what it is, its colour, or where you last saw it.',
  found: 'Thanks for picking it up. What did you find? Tell me what it is, its colour, and where you found it.',
}

export function AskFoundUPage() {
  const { user } = useAuth()
  return user ? <IntakeConversation key={user.id} ownerId={user.id} /> : null
}

function IntakeConversation({ ownerId }: { ownerId: string }) {
  const location = useLocation()
  const navigate = useNavigate()
  const initial = readIntakeHandoff(location.state, ownerId)?.response ?? null
  // Arrived from the bubble on the home page or the feed - a string, nothing more, so it is
  // treated exactly like something typed into the box here.
  const asked = typeof (location.state as { askFoundU?: unknown } | null)?.askFoundU === 'string'
    ? ((location.state as { askFoundU: string }).askFoundU).slice(0, 1000)
    : null
  const [result, setResult] = useState<IntakeResponse | null>(initial)
  // Which side of the counter, when the student has said so with a button. Once the agent has
  // heard it in their words, the answer lives in the slots it sends back.
  const [side, setSide] = useState<IntakeIntent | null>(initial?.slots.intent ?? null)
  const [messages, setMessages] = useState<{ role: 'user' | 'assistant'; text: string }[]>([
    { role: 'assistant', text: initial?.reply ?? GREETING },
  ])
  const [input, setInput] = useState('')
  const finder = side === 'found' || isFinder(result)
  const mutation = useMutation({
    mutationFn: (message: string) => askIntake(message, result?.slots ?? (side ? slotsFor(side) : undefined)),
    onSuccess: response => {
      setResult(response)
      setMessages(previous => [...previous, { role: 'assistant', text: response.reply }])
      setInput('')
    },
  })

  // A question typed into the bubble is sent on arrival rather than left sitting in the box.
  // Someone who asked on the home page has already pressed a button; making them press
  // another one here is the moment the whole thing reads as broken.
  //
  // Scheduled, and cancelled on cleanup, rather than sent straight from the effect. React's
  // development mode mounts the page twice; a mutation started in that window is dropped by
  // its observer, so the reply arrived but "Checking your details" spun for ever. The timer
  // only fires for the mount that stays.
  const sent = useRef(false)
  useEffect(() => {
    if (!asked || sent.current || initial) return
    const timer = setTimeout(() => {
      if (sent.current) return
      sent.current = true
      setMessages(previous => [...previous, { role: 'user', text: asked }])
      mutation.mutate(asked)
      // Clear it from history, so a refresh does not ask the same thing again.
      navigate('.', { replace: true, state: null })
    }, 0)
    return () => clearTimeout(timer)
  }, [asked, initial, mutation, navigate])

  function submit(event: FormEvent) {
    event.preventDefault()
    const message = input.trim()
    if (!message || mutation.isPending) return
    setMessages(previous => previous.at(-1)?.role === 'user' && previous.at(-1)?.text === message
      ? previous : [...previous, { role: 'user', text: message }])
    mutation.mutate(message)
  }

  function reset() {
    mutation.reset()
    setResult(null)
    setSide(null)
    setInput('')
    setMessages([{ role: 'assistant', text: GREETING }])
  }

  function choose(intent: IntakeIntent) {
    setSide(intent)
    setMessages(previous => [...previous,
      { role: 'user', text: intent === 'found' ? 'I found something.' : 'I lost something.' },
      { role: 'assistant', text: GREETINGS[intent] }])
  }

  // The two buttons only make sense before anything has been said.
  const choosing = !result && !side && messages.length === 1 && !mutation.isPending

  return (
    <section className="mx-auto flex w-full max-w-3xl flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-brand-green">Lost it or found it</p>
          <h1 className="pt-1 text-2xl font-semibold tracking-tight">Ask FoundU</h1>
          <p className="max-w-xl pt-2 text-sm text-muted-foreground">
            Tell us what you lost and we’ll check what has been handed in - or what you found, and
            we’ll check who is looking for it. You confirm every post or claim; staff verify ownership.
          </p>
        </div>
        <Button variant="ghost" disabled={mutation.isPending} onClick={reset}>Start again</Button>
      </div>

      <DashboardPanel className="flex flex-col gap-5">
        <ol role="log" aria-label="Conversation with FoundU" aria-live="polite" aria-relevant="additions" className="flex flex-col gap-5">
          {messages.map((message, index) => (
            <li key={index} className={message.role === 'user' ? 'ml-8 rounded-xl bg-muted p-4' : 'flex gap-3'}>
              {message.role === 'assistant' && <BotIcon className="mt-1 size-4 shrink-0 text-brand-green" aria-hidden="true" />}
              <div>
                <p className="text-xs font-medium text-muted-foreground">{message.role === 'user' ? 'You' : 'FoundU'}</p>
                <p className="pt-1 whitespace-pre-wrap text-sm leading-relaxed">{message.text}</p>
              </div>
            </li>
          ))}
        </ol>
        {choosing && (
          <div className="flex flex-wrap gap-2 pl-7">
            <Button variant="outline" onClick={() => choose('lost')}><SearchIcon aria-hidden="true" />I lost something</Button>
            <Button variant="outline" onClick={() => choose('found')}><HandIcon aria-hidden="true" />I found something</Button>
          </div>
        )}
        {mutation.isPending && <p role="status" className="flex items-center gap-2 text-sm text-muted-foreground"><Loader2Icon className="size-4 animate-spin" aria-hidden="true" />Checking your details…</p>}
        {mutation.isError && <p role="alert" className="text-sm text-destructive">
          {mutation.error instanceof ApiError ? mutation.error.message : 'Could not reach FoundU.'} Your message is still below; send it again to retry, or use the report form.
        </p>}
        <PanelDivider />
        <form onSubmit={submit} className="flex flex-col gap-3">
          <Label htmlFor="intake-message">{result ? 'Add or correct a detail' : finder ? 'Describe what you found' : 'Describe your item'}</Label>
          <Textarea id="intake-message" value={input} onChange={event => setInput(event.target.value)}
            maxLength={1000} rows={3} disabled={mutation.isPending} required
            placeholder={finder ? 'I found a blue water bottle in the cafeteria.' : 'I lost a black backpack near the library.'}
            aria-describedby="intake-privacy" />
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p id="intake-privacy" className="text-xs text-muted-foreground">
              {finder
                ? 'Describe the outside only - what is inside is how the owner proves it is theirs.'
                : 'Leave out passwords, ID numbers and other private information.'}
            </p>
            <Button type="submit" disabled={!input.trim() || mutation.isPending}><SendIcon aria-hidden="true" />Send</Button>
          </div>
        </form>
      </DashboardPanel>

      {result && !mutation.isPending && isFinder(result) && (
        <>
          {result.match?.kind === 'lost' && <OwnerLooking match={result.match} />}
          <DashboardPanel className="flex flex-col items-start gap-3">
            <h2 className="text-base font-medium">{result.match ? 'Not the same one? Post it anyway' : 'Your found post draft'}</h2>
            <p className="text-sm text-muted-foreground">{result.draft.description}</p>
            <p className="text-xs text-muted-foreground">
              Nothing has been posted. Check it, then post it so the owner can spot it - or skip the
              post and hand it in at any security desk.
            </p>
            <Button variant="outline" nativeButton={false} render={<Link to="/found/new" state={{ intake: { ownerId, response: result } }} />}>
              Review found post<ArrowRightIcon aria-hidden="true" />
            </Button>
          </DashboardPanel>
        </>
      )}
      {result && !mutation.isPending && !isFinder(result) && (
        <>
          {result.match && <MatchActions key={result.match.id} result={result} ownerId={ownerId} />}
          <DashboardPanel className="flex flex-col items-start gap-3">
            <h2 className="text-base font-medium">{result.match ? 'Need a lost report first?' : 'Your report draft'}</h2>
            <p className="text-sm text-muted-foreground">{result.draft.description}</p>
            {result.draft.when && <p className="text-sm text-muted-foreground">You mentioned “{result.draft.when}”. Check the exact date and time in the form.</p>}
            <p className="text-xs text-muted-foreground">Nothing has been posted. Review the category, place, date and description before sharing it on the public board.</p>
            <Button variant="outline" nativeButton={false} render={<Link to="/my-reports/new" state={{ intake: { ownerId, response: result } }} />}>
              Review report draft<ArrowRightIcon aria-hidden="true" />
            </Button>
          </DashboardPanel>
        </>
      )}
      {finder
        ? <Link to="/found/new" className="self-start text-sm text-muted-foreground underline underline-offset-4">Use the found post form directly</Link>
        : <Link to="/my-reports/new" className="self-start text-sm text-muted-foreground underline underline-offset-4">Use the report form directly</Link>}
    </section>
  )
}

/**
 * A finder shown the report of someone who is looking. The report is already on the public
 * feed; this only points at it. The owner is reached through the report's own "I found this",
 * which is where the warning, the message and the security hand-in code already live.
 */
function OwnerLooking({ match }: { match: NonNullable<IntakeResponse['match']> }) {
  return (
    <DashboardPanel className="flex flex-col items-start gap-4">
      <div>
        <p className="text-sm font-medium text-brand-green">Someone is looking for this · Reported lost</p>
        <h2 className="pt-1 text-xl font-semibold">{[match.colour, match.itemType].filter(Boolean).join(' ')}</h2>
        <p className="flex items-center gap-1 pt-2 text-sm text-muted-foreground"><MapPinIcon className="size-4" aria-hidden="true" />Last seen near {match.location}</p>
        <p className="pt-3 text-sm">{match.description}</p>
      </div>
      <PanelDivider />
      <p className="text-sm text-muted-foreground">
        If it’s what you have, open their report and press <span className="font-medium text-foreground">I found this</span>.
        You can message them, or hand it to security and they get a code to collect it.
      </p>
      <Button nativeButton={false} render={<Link to={`/feed?report=${match.id}`} />}>
        Open their report<ArrowRightIcon aria-hidden="true" />
      </Button>
    </DashboardPanel>
  )
}

function MatchActions({ result, ownerId }: { result: IntakeResponse; ownerId: string }) {
  const match = result.match!
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [page, setPage] = useState(1)
  const [reportId, setReportId] = useState('')
  const [recognised, setRecognised] = useState(false)
  const reports = useQuery({
    queryKey: ['intake-reports', ownerId, page],
    queryFn: () => getMyLostReports({ page, pageSize: 20, status: 'Active', sortBy: 'createdAt', sortDirection: 'desc' }),
  })
  const confirm = useMutation({
    mutationFn: async () => match.kind === 'desk'
      ? { claim: await createClaim(reportId, match.id) }
      : { post: await recogniseFoundPost(match.id, reportId) },
    onSuccess: response => {
      queryClient.invalidateQueries({ queryKey: ['my-lost-reports'] })
      queryClient.invalidateQueries({ queryKey: ['my-claims'] })
      queryClient.invalidateQueries({ queryKey: ['found-feed'] })
      if ('claim' in response && response.claim) navigate(`/claims/${response.claim.id}`)
      else { setRecognised(true); toast.success('The finder has been asked to hand it in.') }
    },
  })
  return (
    <DashboardPanel className="flex flex-col gap-4">
      <div>
        <p className="text-sm font-medium text-brand-green">Possible match · {match.kind === 'desk' ? 'At a desk' : 'Awaiting hand-in'}</p>
        <h2 className="pt-1 text-xl font-semibold">{[match.colour, match.itemType].filter(Boolean).join(' ')}</h2>
        <p className="flex items-center gap-1 pt-2 text-sm text-muted-foreground"><MapPinIcon className="size-4" aria-hidden="true" />{match.location}</p>
        <p className="pt-3 text-sm">{match.description}</p>
      </div>
      <PanelDivider />
      {recognised ? <p role="status" className="text-sm">The finder has been notified. You can claim the item after staff confirm it has reached a desk.</p> : (
        <>
          <p className="text-sm text-muted-foreground">Recognise it? Choose your active lost report before confirming.</p>
          {reports.isPending ? <Skeleton className="h-9 w-full" /> : reports.isError ? (
            <div role="alert"><p className="text-sm">Could not load your reports.</p><Button variant="ghost" onClick={() => reports.refetch()}>Retry reports</Button></div>
          ) : reports.data.items.length === 0 ? <p className="text-sm text-muted-foreground">You have no active reports on this page. Create one using the draft below.</p> : (
            <>
              <Label htmlFor="intake-report">Your lost report</Label>
              <FormSelect id="intake-report" value={reportId} onValueChange={setReportId} placeholder="Choose a report"
                options={reports.data.items.map(report => ({ value: report.id, label: `${report.itemTypeName} · ${report.description.slice(0, 90)}` }))} />
            </>
          )}
          {reports.data && reports.data.totalPages > 1 && <nav aria-label="Your report pages" className="flex items-center justify-between">
            <Button variant="ghost" disabled={page <= 1 || confirm.isPending} onClick={() => { setReportId(''); setPage(page - 1) }}>Previous</Button>
            <span className="text-xs text-muted-foreground">Page {page} of {reports.data.totalPages}</span>
            <Button variant="ghost" disabled={page >= reports.data.totalPages || confirm.isPending} onClick={() => { setReportId(''); setPage(page + 1) }}>Next</Button>
          </nav>}
          {confirm.isError && <p role="alert" className="text-sm text-destructive">{confirm.error instanceof ApiError ? confirm.error.message : 'Could not confirm this item. Please try again.'}</p>}
          <Button className="self-start" disabled={!reportId || confirm.isPending} onClick={() => confirm.mutate()}>
            {confirm.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
            {match.kind === 'desk' ? 'This looks like mine · open a claim' : 'This looks like mine · notify the finder'}
          </Button>
          <p className="text-xs text-muted-foreground">A possible match is not proof of ownership. Staff make the final decision.</p>
        </>
      )}
    </DashboardPanel>
  )
}
