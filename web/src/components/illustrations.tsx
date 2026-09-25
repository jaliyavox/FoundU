import { cn } from '@/lib/utils'

/**
 * 2D monoline illustrations: single stroke weight, no fills, drawn in `currentColor`
 * so they inherit the surrounding text colour and work in both themes untouched.
 *
 * Add the `fu-draw` class plus `is-visible` (see useInView) to have them draw themselves in.
 */

const stroke = {
  fill: 'none',
  stroke: 'currentColor',
  strokeLinecap: 'round',
  strokeLinejoin: 'round',
} as const

interface IllustrationProps {
  className?: string
}

/** Hero: a magnifier over a backpack, with campus odds and ends orbiting it. */
export function SearchSceneIllustration({ className }: IllustrationProps) {
  return (
    <svg
      viewBox="0 0 400 330"
      className={cn('w-full', className)}
      role="img"
      aria-label="A magnifying glass examining a backpack, surrounded by other lost items"
    >
      <g {...stroke} strokeWidth={2.5}>
        {/* orbit */}
        <ellipse cx="200" cy="165" rx="178" ry="140" strokeDasharray="6 12" opacity={0.35} />

        {/* magnifier */}
        <g strokeWidth={3.5}>
          <circle cx="196" cy="152" r="82" />
          <path d="M254 210 L308 264" strokeWidth={9} />
        </g>

        {/* backpack inside the lens */}
        <path d="M166 138 h60 a16 16 0 0 1 16 16 v40 a16 16 0 0 1 -16 16 h-60 a16 16 0 0 1 -16 -16 v-40 a16 16 0 0 1 16 -16 z" />
        <path d="M150 158 q46 -34 92 0" />
        <path d="M176 138 v-10 a20 20 0 0 1 40 0 v10" />
        <rect x="172" y="176" width="48" height="24" rx="9" />
        <path d="M196 176 v24" opacity={0.5} />

        {/* key */}
        <g transform="translate(36 44)">
          <circle cx="16" cy="16" r="11" />
          <path d="M25 22 L52 49" />
          <path d="M42 39 l8 -8" />
          <path d="M48 45 l8 -8" />
        </g>

        {/* phone */}
        <g transform="translate(316 40)">
          <rect x="0" y="0" width="38" height="60" rx="8" />
          <path d="M14 8 h10" />
          <path d="M12 50 h14" opacity={0.5} />
        </g>

        {/* headphones */}
        <g transform="translate(30 236)">
          <path d="M4 34 v-8 a26 26 0 0 1 52 0 v8" />
          <rect x="0" y="32" width="14" height="26" rx="6" />
          <rect x="46" y="32" width="14" height="26" rx="6" />
        </g>

        {/* wallet */}
        <g transform="translate(310 240)">
          <rect x="0" y="0" width="56" height="40" rx="8" />
          <path d="M0 13 h56" opacity={0.5} />
          <circle cx="42" cy="26" r="5" />
        </g>

        {/* sparkles */}
        <g opacity={0.7} strokeWidth={2}>
          <path d="M96 118 v14 M89 125 h14" />
          <path d="M300 154 v11 M294.5 159.5 h11" />
        </g>
      </g>
    </svg>
  )
}

/** Step 1: a report being filled in. */
export function ReportIllustration({ className }: IllustrationProps) {
  return (
    <svg viewBox="0 0 120 110" className={cn('w-full', className)} aria-hidden="true">
      <g {...stroke} strokeWidth={2.5}>
        <rect x="22" y="16" width="62" height="80" rx="10" />
        <rect x="41" y="8" width="24" height="15" rx="6" />
        <path d="M36 46 h34" />
        <path d="M36 60 h34" opacity={0.6} />
        <path d="M36 74 h20" opacity={0.6} />
        <path d="M78 84 l22 -22 a7 7 0 0 1 10 10 l-22 22 -13 3 z" />
      </g>
    </svg>
  )
}

/** Step 2: two records compared and matched. */
export function MatchIllustration({ className }: IllustrationProps) {
  return (
    <svg viewBox="0 0 120 110" className={cn('w-full', className)} aria-hidden="true">
      <g {...stroke} strokeWidth={2.5}>
        <rect x="8" y="24" width="38" height="50" rx="9" />
        <rect x="74" y="24" width="38" height="50" rx="9" />
        <path d="M18 40 h18" opacity={0.6} />
        <path d="M18 52 h12" opacity={0.6} />
        <path d="M84 40 h18" opacity={0.6} />
        <path d="M84 52 h12" opacity={0.6} />
        <path d="M46 49 q14 -22 28 0" strokeDasharray="4 6" />
        <circle cx="60" cy="84" r="15" />
        <path d="M53 84 l5 5 9 -10" />
      </g>
    </svg>
  )
}

/** Step 3: verified, and handed back. */
export function CollectIllustration({ className }: IllustrationProps) {
  return (
    <svg viewBox="0 0 120 110" className={cn('w-full', className)} aria-hidden="true">
      <g {...stroke} strokeWidth={2.5}>
        <rect x="34" y="14" width="52" height="40" rx="8" />
        <path d="M34 28 h52" opacity={0.6} />
        <path d="M60 14 v14" opacity={0.6} />
        <path d="M52 22 q8 -12 16 0" opacity={0.5} />
        <path d="M18 96 q42 -26 84 0" />
        <path d="M30 92 v-14 a8 8 0 0 1 16 0 v10" />
        <path d="M74 88 v-10 a8 8 0 0 1 16 0 v14" />
        <path d="M46 82 h28" opacity={0.5} />
      </g>
    </svg>
  )
}
