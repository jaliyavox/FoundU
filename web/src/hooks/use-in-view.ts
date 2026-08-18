import { useEffect, useRef, useState } from 'react'

/**
 * Reveals an element the first time it scrolls into view, then stops observing.
 * Re-animating on every pass is distracting, so this is deliberately one-shot.
 *
 * Pair with the .fu-reveal / .fu-draw classes in index.css, both of which are
 * disabled under prefers-reduced-motion.
 */
export function useInView<T extends HTMLElement>() {
  const ref = useRef<T>(null)
  const [isVisible, setIsVisible] = useState(false)

  useEffect(() => {
    const node = ref.current
    if (!node) return

    // Without IntersectionObserver, show everything rather than hiding it forever.
    if (typeof IntersectionObserver === 'undefined') {
      setIsVisible(true)
      return
    }

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          setIsVisible(true)
          observer.disconnect()
        }
      },
      { threshold: 0.15, rootMargin: '0px 0px -8% 0px' },
    )

    observer.observe(node)
    return () => observer.disconnect()
  }, [])

  return { ref, isVisible }
}
