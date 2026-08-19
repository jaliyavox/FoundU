import { ClipboardCheckIcon, HandHeartIcon, ShieldCheckIcon, TriangleAlertIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

/**
 * The three hand-in steps, shared by the desktop spotlight (beside the card) and the mobile
 * bottom sheet (inside the panel). Same content either way - only the surface differs, so
 * the two can never drift apart.
 */

const STEPS = [
  { icon: HandHeartIcon, title: 'Hand it in', body: 'Any campus desk will do.' },
  { icon: ClipboardCheckIcon, title: 'Staff log it', body: 'With a detail only the owner knows.' },
  { icon: ShieldCheckIcon, title: 'Owner collects', body: 'They name that detail to claim it.' },
]

export function HandInSteps({ tone }: { tone: 'dark' | 'light' }) {
  const dark = tone === 'dark'

  return (
    <div className="flex flex-col">
      <ol className="flex flex-col gap-3">
        {STEPS.map(({ icon: Icon, title, body }, index) => (
          <li
            key={title}
            className="fu-swap-in relative flex items-center gap-3"
            style={{ animationDelay: `${index * 70}ms` }}
          >
            {/* Rail down to the next node. Anchored to the chip centre and sized past the
                list gap, so it holds whatever the text wraps to. */}
            {index < STEPS.length - 1 && (
              <span
                aria-hidden="true"
                className="fu-rail-in absolute top-1/2 left-4 h-[calc(100%+0.75rem)] w-px bg-linear-to-b from-brand-green/45 to-brand-green/15"
                style={{ animationDelay: `${index * 70 + 120}ms` }}
              />
            )}

            <span
              className={cn(
                'relative z-10 flex size-8 shrink-0 items-center justify-center rounded-lg bg-brand-green/15',
                // The ring punches a gap in the rail, so it connects the nodes
                // rather than running behind them.
                dark ? 'text-brand-green ring-4 ring-[oklch(0.21_0.03_148)]' : 'text-brand-forest ring-4 ring-white',
              )}
            >
              <Icon className="size-4" aria-hidden="true" />
            </span>

            <div className="min-w-0">
              <p className={cn('text-sm font-medium', dark ? 'text-white/90' : 'text-neutral-900')}>
                {title}
              </p>
              <p className={cn('text-sm', dark ? 'text-white/50' : 'text-neutral-500')}>{body}</p>
            </div>
          </li>
        ))}
      </ol>

      <p
        className={cn(
          'fu-swap-in mt-4 flex items-start gap-2 border-t pt-4 text-sm text-pretty',
          dark ? 'border-white/10 text-amber-100/80' : 'border-neutral-900/8 text-neutral-600',
        )}
        style={{ animationDelay: '210ms' }}
      >
        <TriangleAlertIcon
          className={cn('mt-0.5 size-4 shrink-0', dark ? 'text-amber-300' : 'text-amber-600')}
          aria-hidden="true"
        />
        Hand it to a desk, not the owner directly.
      </p>
    </div>
  )
}
