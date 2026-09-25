import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'
import { panelStroke, panelSurface } from './panel-surface'

/**
 * The section container every dashboard page is built from.
 *
 * Dark mode was the problem this solves: flat panels the same value as the page read as one
 * undivided slab, so nothing told the eye where a section started. Each panel is a rounded
 * card lit by a white gradient - strong at the top edge, fading down - plus a hairline
 * highlight along the top. Light and dark share the shape; only the strength of the white
 * changes, so the two themes stay recognisably the same screen.
 *
 * Translucent rather than solid so the page's drifting blooms show through, the way the
 * landing sections do.
 */
export function DashboardPanel({ className, children, ...props }: ComponentProps<'section'>) {
  return (
    <section className={cn(panelSurface, 'p-5 sm:p-6', className)} {...props}>
      <PanelSheen />
      {children}
    </section>
  )
}

/** The stroke along a panel's top edge. Separate so other surfaces can wear it too. */
export function PanelSheen({ className }: { className?: string }) {
  return <span aria-hidden="true" className={cn(panelStroke, className)} />
}

/** The same gradient rule, for dividing content inside one panel. */
export function PanelDivider({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      aria-hidden="true"
      className={cn(
        'h-px w-full shrink-0 bg-linear-to-r from-transparent via-foreground/12 to-transparent dark:via-white/20',
        className,
      )}
      {...props}
    />
  )
}
