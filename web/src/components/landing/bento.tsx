import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

/**
 * Shared shell for the dark bento sections. Keeps borders, gradients, glow and hover
 * behaviour identical everywhere, so new cards inherit the look rather than re-inventing it.
 */

export function DarkSection({
  id,
  children,
  className,
  labelledBy,
}: {
  id?: string
  children: ReactNode
  className?: string
  labelledBy?: string
}) {
  return (
    <section
      id={id}
      aria-labelledby={labelledBy}
      className={cn(
        'relative isolate overflow-hidden bg-[oklch(0.17_0.028_148)] text-white',
        className,
      )}
    >
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10">
        <div className="fu-aurora-b absolute -top-40 right-[12%] size-[34rem] rounded-full bg-brand-forest/50 blur-[130px]" />
        <div className="fu-aurora-c absolute -bottom-40 left-[10%] size-[30rem] rounded-full bg-brand-green/16 blur-[130px]" />
      </div>

      {children}
    </section>
  )
}

export function SectionHeading({
  id,
  eyebrow,
  title,
  body,
}: {
  id: string
  eyebrow?: string
  title: string
  body?: string
}) {
  return (
    <div className="mx-auto flex max-w-2xl flex-col items-center gap-3 text-center">
      {/* Plain text eyebrow - see docs/design.md. Pill badges are not used in this project. */}
      {eyebrow && <p className="text-sm font-medium text-brand-green">{eyebrow}</p>}
      <h2 id={id} className="text-3xl font-semibold tracking-tight text-balance sm:text-5xl">
        {title}
      </h2>
      {body && <p className="text-sm text-pretty text-white/60 sm:text-base">{body}</p>}
    </div>
  )
}

export function BentoCard({
  title,
  body,
  step,
  eyebrow,
  visual,
  align = 'center',
  className,
}: {
  title: string
  body: string
  step?: number
  eyebrow?: string
  visual: ReactNode
  align?: 'center' | 'left'
  className?: string
}) {
  return (
    <article
      className={cn(
        'group relative flex flex-col overflow-hidden rounded-3xl border border-white/10',
        'bg-linear-to-b from-white/[0.07] to-white/[0.015] p-6 backdrop-blur-sm',
        'transition-all duration-500 hover:-translate-y-1 hover:border-brand-green/35',
        className,
      )}
    >
      {/* Corner glow that warms up on hover. */}
      <div
        aria-hidden="true"
        className="pointer-events-none absolute -top-24 -right-16 size-56 rounded-full bg-brand-green/10 opacity-0 blur-3xl transition-opacity duration-500 group-hover:opacity-100"
      />

      <div aria-hidden="true" className="relative flex min-h-40 flex-1 flex-col justify-center overflow-hidden">
        {visual}
      </div>

      <div className={cn('relative pt-6', align === 'center' ? 'text-center' : 'text-left')}>
        {(step !== undefined || eyebrow) && (
          <p className="text-xs font-medium tracking-wide text-brand-green/90 tabular-nums">
            {eyebrow ?? `Step ${step}`}
          </p>
        )}
        <h3 className="pt-1 text-lg font-medium text-white/95">{title}</h3>
        <p
          className={cn(
            'max-w-sm pt-2 text-sm text-pretty text-white/55',
            align === 'center' && 'mx-auto',
          )}
        >
          {body}
        </p>
      </div>
    </article>
  )
}

/**
 * Hairline rule that fades out at both ends. Used between sections instead of a flat
 * border, so the seam reads as a light source rather than a hard line.
 */
export function GradientDivider({ className }: { className?: string }) {
  return (
    <div
      aria-hidden="true"
      className={cn(
        'h-px w-full bg-linear-to-r from-transparent via-white/25 to-transparent',
        className,
      )}
    />
  )
}

/** Vertical counterpart, for separating items inside a row. */
export function VerticalDivider({ className }: { className?: string }) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        'h-8 w-px bg-linear-to-b from-transparent via-white/20 to-transparent',
        className,
      )}
    />
  )
}
