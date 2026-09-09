import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeftIcon,
  CheckIcon,
  ClockIcon,
  Loader2Icon,
  MapPinIcon,
  PlusIcon,
  RotateCwIcon,
  SendIcon,
  ShieldOffIcon,
  Trash2Icon,
  UndoIcon,
} from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider, PanelSheen } from '@/components/layout/dashboard-panel'
import { panelSurface } from '@/components/layout/panel-surface'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { useAuth } from '@/features/auth/use-auth'
import { formatDateTime } from '@/features/reports/reports-api'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import {
  addQuestions,
  cancelClaim,
  CLAIM_STATUS_COPY,
  decideClaim,
  getClaim,
  submitAnswers,
  type ClaimDetail,
} from './claims-api'
import { ClaimStatusChip } from './claim-status-chip'

/**
 * One claim, for whoever is allowed to see it.
 *
 * Student and staff read the same record from opposite sides - the student answers, staff ask
 * and decide - so this is one screen with two sets of controls rather than two screens that
 * would drift apart. The API enforces which side you are on; this only decides what to show.
 */
export function ClaimDetailPage() {
  const { id = '' } = useParams()
  const { user } = useAuth()
  const isStaff = user?.role === 'Staff' || user?.role === 'Admin'

  const { data: claim, isPending, isError, error, refetch } = useQuery({
    queryKey: ['claim', id],
    queryFn: () => getClaim(id),
  })

  if (isPending) {
    return (
      <section className="mx-auto flex w-full max-w-3xl flex-col gap-6">
        <Skeleton className="h-7 w-64" />
        <DashboardPanel className="flex flex-col gap-4">
          <Skeleton className="h-4 w-40" />
          <Skeleton className="h-3 w-full" />
          <Skeleton className="h-3 w-2/3" />
        </DashboardPanel>
      </section>
    )
  }

  if (isError) {
    return (
      <DashboardPanel
        role="alert"
        className="mx-auto flex w-full max-w-3xl flex-col items-start gap-3 border-destructive/40 from-destructive/8 via-destructive/5 to-transparent dark:from-destructive/15 dark:via-destructive/8"
      >
        <div>
          <p className="font-heading text-base font-medium">Could not load this claim</p>
          <p className="pt-1 text-sm text-muted-foreground">
            {error instanceof ApiError ? error.message : 'Check the API is running.'}
          </p>
        </div>
        <Button variant="outline" onClick={() => refetch()}>
          <RotateCwIcon aria-hidden="true" />
          Try again
        </Button>
      </DashboardPanel>
    )
  }

  const copy = CLAIM_STATUS_COPY[claim.status]

  return (
    <section className="mx-auto flex w-full max-w-3xl flex-col gap-6">
      <Button
        variant="ghost"
        size="sm"
        className="self-start text-muted-foreground"
        nativeButton={false}
        render={<Link to={isStaff ? '/claims' : '/my-claims'} />}
      >
        <ArrowLeftIcon aria-hidden="true" />
        {isStaff ? 'Back to the queue' : 'Back to my claims'}
      </Button>

      <DashboardPanel className="flex flex-col gap-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">{claim.foundItem.itemTypeName}</h1>
            <p className="pt-1 text-sm text-muted-foreground">
              {isStaff
                ? `Claimed by ${claim.studentName} · ${formatDateTime(claim.createdAt)}`
                : copy.student}
            </p>
          </div>
          <ClaimStatusChip status={claim.status} />
        </div>

        {claim.decision && (
          <>
            <PanelDivider />
            <div className="flex flex-col gap-1">
              <p className="text-sm font-medium">
                {claim.decision === 'Approved'
                  ? 'Approved'
                  : claim.decision === 'Rejected'
                    ? 'Not approved'
                    : 'Sent back for more detail'}
                {claim.decidedByName && (
                  <span className="font-normal text-muted-foreground"> by {claim.decidedByName}</span>
                )}
              </p>
              {claim.decisionReason && (
                <p className="text-sm text-pretty text-muted-foreground">{claim.decisionReason}</p>
              )}
            </div>
          </>
        )}
      </DashboardPanel>

      <ItemPanel claim={claim} />

      <QuestionsPanel claim={claim} isStaff={isStaff} />

      {isStaff ? <StaffControls claim={claim} /> : <StudentControls claim={claim} />}
    </section>
  )
}

/* -------------------------------------------------------------------- pieces */

/** The item as both sides may see it - the student-safe shape, no hidden evidence. */
function ItemPanel({ claim }: { claim: ClaimDetail }) {
  return (
    <DashboardPanel className="flex flex-col gap-4">
      <h2 className="font-heading text-base font-medium">The item</h2>

      <p className="text-sm text-pretty text-muted-foreground">{claim.foundItem.generalDescription}</p>

      <dl className="grid gap-3 sm:grid-cols-2">
        <div className="flex items-start gap-3">
          <MapPinIcon className="mt-0.5 size-4 shrink-0 text-brand-green" aria-hidden="true" />
          <div>
            <dt className="text-xs text-muted-foreground">Handed in at</dt>
            <dd className="text-sm">{claim.foundItem.foundLocationName}</dd>
          </div>
        </div>
        <div className="flex items-start gap-3">
          <ClockIcon className="mt-0.5 size-4 shrink-0 text-brand-green" aria-hidden="true" />
          <div>
            <dt className="text-xs text-muted-foreground">Found</dt>
            <dd className="text-sm">{formatDateTime(claim.foundItem.foundAt)}</dd>
          </div>
        </div>
      </dl>

      <PanelDivider />

      <div>
        <p className="text-xs text-muted-foreground">Claimed against your report</p>
        <p className="text-sm text-pretty">{claim.lostReportDescription}</p>
      </div>
    </DashboardPanel>
  )
}

/**
 * The questions, and the answers when they exist.
 *
 * A student who still owes answers gets a form; everyone else reads the record. Whether an
 * answer was judged correct is never shown - that would hand a false claimant the feedback
 * loop they need to guess the rest.
 */
function QuestionsPanel({ claim, isStaff }: { claim: ClaimDetail; isStaff: boolean }) {
  const queryClient = useQueryClient()
  const [drafts, setDrafts] = useState<Record<string, string>>(() =>
    Object.fromEntries(claim.questions.map((q) => [q.id, q.answerText ?? ''])),
  )
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  const canAnswer =
    !isStaff && (claim.status === 'WaitingForAnswer' || claim.status === 'RevisionRequested')

  const answer = useMutation({
    mutationFn: () =>
      submitAnswers(
        claim.id,
        claim.questions.map((q) => ({ questionId: q.id, answerText: drafts[q.id]?.trim() ?? '' })),
      ),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['claim', claim.id] })
      queryClient.invalidateQueries({ queryKey: ['my-claims'] })
      toast.success('Sent to the desk.')
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setFieldErrors(error.fieldErrors)
        if (Object.keys(error.fieldErrors).length === 0) toast.error(error.message)
      } else {
        toast.error('Could not reach the server.')
      }
    },
  })

  if (claim.questions.length === 0) {
    return (
      <DashboardPanel className="flex flex-col gap-2">
        <h2 className="font-heading text-base font-medium">Verification</h2>
        <p className="text-sm text-muted-foreground">
          {isStaff
            ? 'No questions yet. Ask something only the owner could answer - from the detail that was never published.'
            : 'The desk has not sent any questions yet. You will see them here.'}
        </p>
      </DashboardPanel>
    )
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFieldErrors({})
    answer.mutate()
  }

  return (
    <DashboardPanel className="flex flex-col gap-5">
      <div>
        <h2 className="font-heading text-base font-medium">Verification</h2>
        <p className="pt-1 text-sm text-muted-foreground">
          {canAnswer
            ? 'Answer from memory. Staff are comparing this against what they can see and you cannot.'
            : `${claim.questions.length} question${claim.questions.length === 1 ? '' : 's'} asked.`}
        </p>
      </div>

      {canAnswer ? (
        <form onSubmit={handleSubmit} className="flex flex-col gap-5">
          {claim.questions.map((question, index) => (
            <div key={question.id} className="flex flex-col gap-2">
              <Label htmlFor={`answer-${question.id}`}>
                {index + 1}. {question.questionText}
              </Label>
              <Textarea
                id={`answer-${question.id}`}
                rows={2}
                required
                value={drafts[question.id] ?? ''}
                onChange={(event) =>
                  setDrafts((current) => ({ ...current, [question.id]: event.target.value }))
                }
              />
            </div>
          ))}

          {fieldErrors.Answers && (
            <p className="text-sm text-destructive">{fieldErrors.Answers.join(' ')}</p>
          )}

          <Button
            type="submit"
            disabled={answer.isPending}
            className="self-start bg-brand-forest text-white hover:bg-brand-forest/90"
          >
            {answer.isPending ? (
              <Loader2Icon className="animate-spin" aria-hidden="true" />
            ) : (
              <SendIcon aria-hidden="true" />
            )}
            Send my answers
          </Button>
        </form>
      ) : (
        <ol className="flex flex-col gap-4">
          {claim.questions.map((question, index) => (
            <li key={question.id} className="flex flex-col gap-1">
              <p className="text-sm font-medium">
                {index + 1}. {question.questionText}
              </p>
              {question.answerText ? (
                <p className="border-l-2 border-brand-green/40 pl-3 text-sm text-pretty text-muted-foreground">
                  {question.answerText}
                </p>
              ) : (
                <p className="pl-3 text-sm text-muted-foreground italic">Not answered yet</p>
              )}
            </li>
          ))}
        </ol>
      )}
    </DashboardPanel>
  )
}

/* ------------------------------------------------------------------ student */

function StudentControls({ claim }: { claim: ClaimDetail }) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [confirming, setConfirming] = useState(false)

  const isOpen = !['Approved', 'Rejected', 'Cancelled'].includes(claim.status)

  const cancel = useMutation({
    mutationFn: () => cancelClaim(claim.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['claim', claim.id] })
      queryClient.invalidateQueries({ queryKey: ['my-claims'] })
      queryClient.invalidateQueries({ queryKey: ['my-lost-reports'] })
      toast.success('Claim cancelled.')
      navigate('/my-claims')
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : 'Could not reach the server.'),
  })

  if (!isOpen) return null

  return (
    <div className={cn(panelSurface, 'flex flex-wrap items-center gap-3 p-4')}>
      <PanelSheen />
      {confirming ? (
        <>
          <p className="relative text-sm text-muted-foreground">
            Give up this claim? The item stays with the desk.
          </p>
          <Button size="sm" variant="destructive" onClick={() => cancel.mutate()} disabled={cancel.isPending}>
            {cancel.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
            Yes, cancel it
          </Button>
          <Button size="sm" variant="ghost" onClick={() => setConfirming(false)}>
            Keep it
          </Button>
        </>
      ) : (
        <Button
          size="sm"
          variant="ghost"
          className="relative text-muted-foreground"
          onClick={() => setConfirming(true)}
        >
          <Trash2Icon aria-hidden="true" />
          Cancel this claim
        </Button>
      )}
    </div>
  )
}

/* -------------------------------------------------------------------- staff */

function StaffControls({ claim }: { claim: ClaimDetail }) {
  const queryClient = useQueryClient()
  const [questions, setQuestions] = useState<string[]>([''])
  const [reason, setReason] = useState('')
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  const isOpen = !['Approved', 'Rejected', 'Cancelled'].includes(claim.status)

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['claim', claim.id] })
    queryClient.invalidateQueries({ queryKey: ['claim-queue'] })
  }

  const ask = useMutation({
    mutationFn: () => addQuestions(claim.id, questions.map((q) => q.trim()).filter(Boolean)),
    onSuccess: () => {
      setQuestions([''])
      refresh()
      toast.success('Sent to the claimant.')
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setFieldErrors(error.fieldErrors)
        if (Object.keys(error.fieldErrors).length === 0) toast.error(error.message)
      } else {
        toast.error('Could not reach the server.')
      }
    },
  })

  const decide = useMutation({
    mutationFn: (decision: string) => decideClaim(claim.id, decision, reason.trim() || undefined),
    onSuccess: (updated) => {
      setReason('')
      refresh()
      toast.success(
        updated.status === 'Approved'
          ? 'Approved. The item is marked returned and the report resolved.'
          : updated.status === 'Rejected'
            ? 'Rejected.'
            : 'Sent back for more detail.',
      )
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setFieldErrors(error.fieldErrors)
        if (Object.keys(error.fieldErrors).length === 0) toast.error(error.message)
      } else {
        toast.error('Could not reach the server.')
      }
    },
  })

  if (!isOpen) return null

  const isBusy = ask.isPending || decide.isPending

  return (
    <>
      <DashboardPanel className="flex flex-col gap-4">
        <div>
          <h2 className="font-heading text-base font-medium">Ask a verification question</h2>
          <p className="pt-1 text-sm text-muted-foreground">
            Write it from the item's hidden detail, and never give the answer away in the
            question. Up to five at a time.
          </p>
        </div>

        <div className="flex flex-col gap-2">
          {questions.map((question, index) => (
            <Input
              key={index}
              value={question}
              placeholder={
                index === 0 ? 'What is in the front pocket, and what colour is it?' : 'Another question'
              }
              onChange={(event) =>
                setQuestions((current) =>
                  current.map((q, i) => (i === index ? event.target.value : q)),
                )
              }
            />
          ))}
        </div>

        {fieldErrors.Questions && (
          <p className="text-sm text-destructive">{fieldErrors.Questions.join(' ')}</p>
        )}

        <div className="flex flex-wrap gap-2">
          {questions.length < 5 && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => setQuestions((current) => [...current, ''])}
              disabled={isBusy}
            >
              <PlusIcon aria-hidden="true" />
              Add another
            </Button>
          )}
          <Button
            size="sm"
            className="bg-brand-forest text-white hover:bg-brand-forest/90"
            onClick={() => ask.mutate()}
            disabled={isBusy || questions.every((q) => !q.trim())}
          >
            {ask.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
            Send to the claimant
          </Button>
        </div>
      </DashboardPanel>

      <DashboardPanel className="flex flex-col gap-4">
        <div>
          <h2 className="font-heading text-base font-medium">Decide</h2>
          <p className="pt-1 text-sm text-muted-foreground">
            Approving hands the item over: it is marked returned and the student's report is
            resolved. Any other open claim on this item is closed with a reason.
          </p>
        </div>

        <div className="flex flex-col gap-2">
          <Label htmlFor="decision-reason">
            Reason <span className="text-muted-foreground">- required unless approving</span>
          </Label>
          <Textarea
            id="decision-reason"
            rows={2}
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            placeholder="The keychain described does not match what is in the bag."
            aria-invalid={Boolean(fieldErrors.Reason)}
          />
          {fieldErrors.Reason && (
            <p className="text-sm text-destructive">{fieldErrors.Reason.join(' ')}</p>
          )}
        </div>

        <div className="flex flex-wrap gap-2">
          <Button
            className="bg-brand-forest text-white hover:bg-brand-forest/90"
            onClick={() => decide.mutate('Approved')}
            disabled={isBusy}
          >
            {decide.isPending && decide.variables === 'Approved' && (
              <Loader2Icon className="animate-spin" aria-hidden="true" />
            )}
            <CheckIcon aria-hidden="true" />
            Approve
          </Button>
          <Button variant="outline" onClick={() => decide.mutate('RevisionRequested')} disabled={isBusy}>
            {decide.isPending && decide.variables === 'RevisionRequested' && (
              <Loader2Icon className="animate-spin" aria-hidden="true" />
            )}
            <UndoIcon aria-hidden="true" />
            Ask for more detail
          </Button>
          <Button variant="destructive" onClick={() => decide.mutate('Rejected')} disabled={isBusy}>
            {decide.isPending && decide.variables === 'Rejected' && (
              <Loader2Icon className="animate-spin" aria-hidden="true" />
            )}
            <ShieldOffIcon aria-hidden="true" />
            Reject
          </Button>
        </div>
      </DashboardPanel>
    </>
  )
}
