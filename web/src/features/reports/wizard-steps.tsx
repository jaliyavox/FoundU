import { CheckIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

/**
 * Numbered progress header. Completed steps become ticks, so at a glance you can see how
 * much is behind you as well as what is left.
 */
export function WizardSteps({ steps, current }: { steps: { label: string }[]; current: number }) {
  return (
    <ol className="flex flex-wrap items-center gap-x-3 gap-y-2">
      {steps.map(({ label }, index) => {
        const isDone = index < current
        const isCurrent = index === current

        return (
          <li key={label} className="flex items-center gap-3">
            <span className="flex items-center gap-2.5">
              <span
                aria-hidden="true"
                className={cn(
                  'flex size-7 shrink-0 items-center justify-center rounded-full text-xs font-medium transition-colors duration-300',
                  isDone && 'bg-brand-green text-white',
                  isCurrent && 'bg-primary text-primary-foreground',
                  !isDone && !isCurrent && 'bg-muted text-muted-foreground',
                )}
              >
                {isDone ? <CheckIcon className="size-3.5" strokeWidth={3} /> : index + 1}
              </span>

              <span
                aria-current={isCurrent ? 'step' : undefined}
                className={cn(
                  'text-sm transition-colors duration-300',
                  isCurrent ? 'font-medium text-foreground' : 'text-muted-foreground',
                )}
              >
                {label}
              </span>
            </span>

            {index < steps.length - 1 && (
              <span
                aria-hidden="true"
                className={cn(
                  'hidden h-px w-8 transition-colors duration-300 sm:block',
                  isDone ? 'bg-brand-green' : 'bg-border',
                )}
              />
            )}
          </li>
        )
      })}
    </ol>
  )
}
