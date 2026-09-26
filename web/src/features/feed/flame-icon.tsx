import { useId } from 'react'

/**
 * The "Fresh finds" flame: an outer body, a hot inner core and a halo, each on its own
 * cycle so the flicker never reads as a loop.
 *
 * Drawn rather than imported because the lucide flame is a single outline path - it can be
 * scaled, but not lit from the inside. The three animations live in index.css and switch
 * off under prefers-reduced-motion, leaving a still flame.
 */
export function FlameIcon({ className }: { className?: string }) {
  // Gradient ids must be unique per instance, or a second icon on the page steals the first
  // one's fill in some browsers.
  const uid = useId().replace(/:/g, '')

  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className={className}>
      <defs>
        <linearGradient id={`${uid}-body`} x1="12" y1="2" x2="12" y2="20" gradientUnits="userSpaceOnUse">
          <stop offset="0" stopColor="#FFC24A" />
          <stop offset="0.45" stopColor="#F97316" />
          <stop offset="1" stopColor="#DC2626" />
        </linearGradient>
        <linearGradient id={`${uid}-core`} x1="12" y1="10" x2="12" y2="18" gradientUnits="userSpaceOnUse">
          <stop offset="0" stopColor="#FFF7DC" />
          <stop offset="1" stopColor="#FFC24A" />
        </linearGradient>
        {/* Fades to nothing, so the heat around the flame has no edge of its own. */}
        <radialGradient id={`${uid}-halo`} cx="12" cy="14" r="9" gradientUnits="userSpaceOnUse">
          <stop offset="0" stopColor="#FB923C" stopOpacity="0.5" />
          <stop offset="0.6" stopColor="#FB923C" stopOpacity="0.16" />
          <stop offset="1" stopColor="#FB923C" stopOpacity="0" />
        </radialGradient>
      </defs>

      <circle className="fu-flame-glow" cx="12" cy="14" r="9" fill={`url(#${uid}-halo)`} />

      <path
        className="fu-flame-body"
        fill={`url(#${uid}-body)`}
        d="M12 2.2c.5 2.6-.7 4-2 5.3-1.6 1.6-3.5 3.2-3.5 6.2A5.6 5.6 0 0 0 12 19.4a5.6 5.6 0 0 0 5.5-5.7c0-2.3-1-4-2.2-5.6-.5.7-1.1 1.2-1.8 1.4.7-2.6-.2-5.3-1.5-7.3Z"
      />

      {/* The hot centre: brighter, quicker and shorter than the body. */}
      <path
        className="fu-flame-core"
        fill={`url(#${uid}-core)`}
        d="M12 10.6c1.3 1.3 2 2.4 2 3.6a2 2 0 0 1-4 .1c0-1.4.8-2.4 2-3.7Z"
      />
    </svg>
  )
}
