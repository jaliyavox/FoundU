import { cn } from '@/lib/utils'
import { CLAIM_STATUS_COPY, TONE_STYLES, type ClaimStatus } from './claims-api'

/** Status as a shape as well as a word, so the queue reads at a glance. */
export function ClaimStatusChip({ status, className }: { status: ClaimStatus; className?: string }) {
  const { label, tone } = CLAIM_STATUS_COPY[status]

  return (
    <span
      className={cn(
        'inline-flex w-fit items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium whitespace-nowrap',
        TONE_STYLES[tone],
        className,
      )}
    >
      <span className="size-1.5 rounded-full bg-current" aria-hidden="true" />
      {label}
    </span>
  )
}
