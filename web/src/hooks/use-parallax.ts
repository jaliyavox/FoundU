import { useEffect, useRef, useState } from 'react'

/**
 * Reports how far an element is through the viewport, from 1 (just below the fold) through
 * 0 (dead centre) to -1 (just above it). Multiply by a distance to move a layer.
 *
 * Scroll handling is throttled to one rAF per frame, and skipped entirely under
 * prefers-reduced-motion, where every layer simply stays at rest.
 */
export function useParallax<T extends HTMLElement>() {
  const ref = useRef<T>(null)
  const [progress, setProgress] = useState(0)

  useEffect(() => {
    const node = ref.current
    if (!node) return
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return

    let frame = 0

    const update = () => {
      frame = 0
      const rect = node.getBoundingClientRect()
      const viewport = window.innerHeight
      const centre = rect.top + rect.height / 2
      const raw = (centre - viewport / 2) / (viewport / 2 + rect.height / 2)
      setProgress(Math.max(-1, Math.min(1, raw)))
    }

    const onScroll = () => {
      if (!frame) frame = window.requestAnimationFrame(update)
    }

    update()
    window.addEventListener('scroll', onScroll, { passive: true })
    window.addEventListener('resize', onScroll)

    return () => {
      if (frame) window.cancelAnimationFrame(frame)
      window.removeEventListener('scroll', onScroll)
      window.removeEventListener('resize', onScroll)
    }
  }, [])

  return { ref, progress }
}
