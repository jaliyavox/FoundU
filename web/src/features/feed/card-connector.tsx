import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'

/**
 * Dashed lines joining the spotlit card to whatever comes next: the side panel normally,
 * and card -> steps -> panel once the hand-in flow is open. It measures the spotlight (a
 * fixed position), never the card in the grid, which could be anywhere - including under
 * the panel.
 *
 * Rendered into document.body: the feed sits inside sections with their own stacking
 * contexts, and this has to clear the sheet's backdrop (z-50).
 *
 * Re-measured every frame while open - the panel slides in under a transform, the steps
 * animate in, and either can move with a resize.
 */

const GREEN = 'oklch(0.721 0.141 146.1)'

interface Segment {
  x1: number
  y1: number
  x2: number
  y2: number
}

function segmentBetween(from: Element, to: Element): Segment {
  const a = from.getBoundingClientRect()
  const b = to.getBoundingClientRect()
  return {
    x1: a.right,
    y1: a.top + a.height / 2,
    x2: b.left,
    // The panel is tall; aim at its upper area rather than its centre so the line stays flat.
    y2: b.top + Math.min(b.height / 2, 220),
  }
}

export function CardConnector({
  cardId,
  showHandIn,
}: {
  cardId: string | null
  showHandIn: boolean
}) {
  const [segments, setSegments] = useState<Segment[]>([])

  useEffect(() => {
    if (!cardId) {
      setSegments([])
      return
    }

    // Below `sm` the panel is full width and covers the feed, so there is nothing to join.
    if (window.matchMedia('(max-width: 639px)').matches) return
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return

    let frame = 0

    const measure = () => {
      const card = document.querySelector(`[data-feed-spotlight="${cardId}"]`)
      const steps = document.querySelector('[data-feed-steps]')
      const panel = document.querySelector('[data-slot="sheet-content"]')

      if (card && panel) {
        // Chain through the steps when they are showing, so the eye follows the flow.
        setSegments(
          showHandIn && steps
            ? [segmentBetween(card, steps), segmentBetween(steps, panel)]
            : [segmentBetween(card, panel)],
        )
      }

      frame = window.requestAnimationFrame(measure)
    }

    measure()
    return () => window.cancelAnimationFrame(frame)
  }, [cardId, showHandIn])

  if (segments.length === 0) return null

  return createPortal(
    <svg
      aria-hidden="true"
      className="pointer-events-none fixed inset-0 z-55"
      style={{ width: '100vw', height: '100vh' }}
    >
      {segments.map(({ x1, y1, x2, y2 }, index) => {
        const midX = x1 + (x2 - x1) / 2
        const path = `M ${x1} ${y1} C ${midX} ${y1}, ${midX} ${y2}, ${x2} ${y2}`

        return (
          <g key={index}>
            {/* Halo, so the line stays readable over the blurred backdrop. */}
            <path d={path} fill="none" stroke={GREEN} strokeWidth="6" strokeOpacity="0.16" strokeLinecap="round" />
            <path className="fu-flow" d={path} fill="none" stroke={GREEN} strokeWidth="2" strokeLinecap="round" />
            <circle cx={x1} cy={y1} r="4" fill={GREEN} />
            <circle cx={x2} cy={y2} r="4" fill={GREEN} />
          </g>
        )
      })}
    </svg>,
    document.body,
  )
}
