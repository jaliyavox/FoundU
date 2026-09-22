import { useQuery } from '@tanstack/react-query'
import { BotIcon, CircleAlertIcon, CircleCheckIcon, Loader2Icon, PauseIcon } from 'lucide-react'
import { DashboardPanel, PanelDivider } from '@/components/layout/dashboard-panel'
import { Skeleton } from '@/components/ui/skeleton'
import { formatDateTime } from '@/features/reports/reports-api'
import { cn } from '@/lib/utils'
import { getAgentRuns, type AgentRun } from './claims-api'

/**
 * What the AI did on the way to this claim, for the staff member deciding it.
 *
 * It reads as a log, oldest at the bottom: the matching run that put the item in front of
 * the student, then each verification run. Only the auditable outcome is shown - the API
 * never sends reasoning, hidden evidence or the student's answers here - and the panel is
 * staff-only, because a student must not learn which way the agent leaned before a person
 * decides.
 */
export function AgentRunsPanel({ claimId }: { claimId: string }) {
  const { data, isPending, isError } = useQuery({
    queryKey: ['claim-agent-runs', claimId],
    queryFn: () => getAgentRuns(claimId),
  })

  return (
    <DashboardPanel className="flex flex-col gap-4">
      <div className="flex items-center gap-2">
        <BotIcon className="size-4 text-muted-foreground" aria-hidden="true" />
        <h2 className="font-heading text-base font-medium">What the agents did</h2>
      </div>

      <PanelDivider />

      {isPending ? (
        <div className="flex flex-col gap-3">
          <Skeleton className="h-4 w-2/3" />
          <Skeleton className="h-4 w-1/2" />
        </div>
      ) : isError ? (
        <p className="text-sm text-muted-foreground">Could not load the agent trail.</p>
      ) : data.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          No agent has touched this claim. The item was linked and the questions written by
          hand.
        </p>
      ) : (
        <ol className="flex flex-col">
          {data.map((run, index) => (
            <li
              key={run.id}
              className={cn(
                'relative flex gap-3 pb-5 pl-1',
                // The rail joins the runs into one trail; the last entry has nothing below it.
                index < data.length - 1 &&
                  'before:absolute before:top-6 before:bottom-0 before:left-[11px] before:w-px before:bg-foreground/10',
              )}
            >
              <StatusDot status={run.status} />

              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
                  <span className="text-sm font-medium">{AGENT_LABELS[run.agent]}</span>
                  <span className="text-xs text-muted-foreground">{run.objective}</span>
                </div>

                <OutcomeLines run={run} />

                <p className="pt-1 text-xs text-muted-foreground">
                  {formatDateTime(run.startedAt)}
                  {run.completedAt && run.status !== 'Running' && ` · ${STATUS_LABELS[run.status]}`}
                </p>
              </div>
            </li>
          ))}
        </ol>
      )}
    </DashboardPanel>
  )
}

const AGENT_LABELS: Record<AgentRun['agent'], string> = {
  Matching: 'Matching agent',
  Verification: 'Verification agent',
  DescriptionParsing: 'Description parser',
  Planner: 'Planner',
}

const STATUS_LABELS: Record<AgentRun['status'], string> = {
  Running: 'running',
  PausedForApproval: 'waiting for a person',
  Completed: 'completed',
  Failed: 'failed',
}

function StatusDot({ status }: { status: AgentRun['status'] }) {
  const base = 'relative mt-0.5 flex size-[22px] shrink-0 items-center justify-center rounded-full border bg-card'
  switch (status) {
    case 'Completed':
      return (
        <span className={cn(base, 'border-brand-green/40 text-brand-forest dark:text-brand-sage')}>
          <CircleCheckIcon className="size-3.5" aria-hidden="true" />
        </span>
      )
    case 'Failed':
      return (
        <span className={cn(base, 'border-destructive/40 text-destructive')}>
          <CircleAlertIcon className="size-3.5" aria-hidden="true" />
        </span>
      )
    case 'PausedForApproval':
      return (
        <span className={cn(base, 'border-amber-500/40 text-amber-700 dark:text-amber-300')}>
          <PauseIcon className="size-3.5" aria-hidden="true" />
        </span>
      )
    default:
      return (
        <span className={cn(base, 'border-foreground/15 text-muted-foreground')}>
          <Loader2Icon className="size-3.5 animate-spin" aria-hidden="true" />
        </span>
      )
  }
}

/**
 * The few outcome fields worth a sentence. The outcome object is the service's own safe
 * audit shape; anything it carries beyond these is kept out of the way rather than dumped
 * as JSON on a page a person reads over a counter.
 */
function OutcomeLines({ run }: { run: AgentRun }) {
  const o = run.outcome ?? {}
  const lines: string[] = []

  if (run.errorMessage) lines.push(run.errorMessage)

  const recommendation = pick(o, 'recommendation')
  if (recommendation) lines.push(`Recommended: ${humanise(recommendation)}`)

  const score = o.score
  if (typeof score === 'number') lines.push(`Confidence ${Math.round(score * 100)}%`)

  const questions = o.questions
  if (Array.isArray(questions) && questions.length > 0) {
    lines.push(`Drafted ${questions.length} question${questions.length === 1 ? '' : 's'}`)
  }

  const operation = pick(o, 'operation')
  if (operation && !run.objective.toLowerCase().includes(operation.toLowerCase())) {
    lines.push(humanise(operation))
  }

  if (o.outcome === 'unavailable') lines.push('The agent service could not be reached; staff continued by hand.')

  if (lines.length === 0) return null

  return (
    <ul className="pt-1 text-sm text-pretty text-muted-foreground">
      {lines.map((line) => (
        <li key={line}>{line}</li>
      ))}
    </ul>
  )
}

/** A string field, case-insensitively - the outcome shapes vary between agents. */
function pick(o: Record<string, unknown>, key: string): string | null {
  const found = Object.entries(o).find(([k]) => k.toLowerCase() === key.toLowerCase())
  return found && typeof found[1] === 'string' ? found[1] : null
}

/** "REQUEST_REVISION" or "requestRevision" -> "request revision". */
const humanise = (value: string) =>
  value
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .toLowerCase()
