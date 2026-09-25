import { cn } from '@/lib/utils'

/**
 * FoundU brand mark: a crate of handed-in items with a magnifier asking whose it is.
 *
 * The mark is a fixed brand lockup - forest tile, mist artwork - in both themes, the way a
 * printed logo would be. It does not follow the semantic colour tokens, so it stays
 * recognisable wherever it appears. The lens is knocked out of the crate with `evenodd`,
 * so the tile shows through rather than being painted a third colour.
 */

interface MarkProps {
  className?: string
  /** Set when the logo is decorative because a visible "FoundU" wordmark sits beside it. */
  decorative?: boolean
}

export function FoundUMark({ className, decorative = false }: MarkProps) {
  return (
    <svg
      viewBox="0 0 64 64"
      className={cn('size-9 shrink-0', className)}
      role={decorative ? undefined : 'img'}
      aria-label={decorative ? undefined : 'FoundU'}
      aria-hidden={decorative || undefined}
    >
      <rect width="64" height="64" rx="14" fill="var(--brand-forest)" />

      <g fill="var(--brand-mist)">
        {/* Items spilling out of the crate. */}
        <g transform="rotate(-24 22 24)">
          <rect x="16.5" y="14" width="11" height="18" rx="2.5" />
        </g>
        <g transform="rotate(-9 29 23)">
          <rect x="25.5" y="13.5" width="8" height="17" rx="2" />
        </g>
        <g transform="rotate(19 41 23)">
          <rect x="34.5" y="17" width="13.5" height="12.5" rx="2.5" />
          <path
            d="M38 17.5 a3.4 3.4 0 0 1 6.8 0"
            fill="none"
            stroke="var(--brand-mist)"
            strokeWidth="2"
            strokeLinecap="round"
          />
        </g>

        {/* Crate, with the lens knocked out of it. */}
        <path
          fillRule="evenodd"
          d="M14.5 29.5 h35 a2.5 2.5 0 0 1 2.5 2.5 v18 a2.5 2.5 0 0 1 -2.5 2.5 h-35 a2.5 2.5 0 0 1 -2.5 -2.5 v-18 a2.5 2.5 0 0 1 2.5 -2.5 z
             M31.4 33.4 a8.4 8.4 0 1 0 0 16.8 a8.4 8.4 0 1 0 0 -16.8 z"
        />

        {/* Lens ring, question mark, and handle. */}
        <circle
          cx="31.4"
          cy="41.8"
          r="6.6"
          fill="none"
          stroke="var(--brand-mist)"
          strokeWidth="2.2"
        />
        <path
          d="M29.2 39.6 a2.3 2.3 0 1 1 2.5 3 v1"
          fill="none"
          stroke="var(--brand-mist)"
          strokeWidth="1.9"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        <circle cx="31.5" cy="45.6" r="1.1" />
        <path
          d="M36.6 46.9 L41.6 51.9"
          fill="none"
          stroke="var(--brand-mist)"
          strokeWidth="3.2"
          strokeLinecap="round"
        />
      </g>
    </svg>
  )
}

/** Mark plus wordmark, for headers and the sidebar. */
export function FoundULogo({
  className,
  markClassName,
  showTagline = false,
}: {
  className?: string
  markClassName?: string
  showTagline?: boolean
}) {
  return (
    <span className={cn('flex items-center gap-2.5', className)}>
      <FoundUMark decorative className={markClassName} />
      <span className="grid leading-tight">
        <span className="text-base font-semibold tracking-tight">FoundU</span>
        {showTagline && (
          <span className="truncate text-xs text-muted-foreground">Campus lost &amp; found</span>
        )}
      </span>
    </span>
  )
}
