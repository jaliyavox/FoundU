import { useEffect, useState } from 'react'

/**
 * Cycles 0..length-1 on an interval, pausing when the tab is hidden so a background
 * tab is not animating for nothing. Respects prefers-reduced-motion by holding still.
 */
export function useRotatingIndex(length: number, intervalMs = 3200) {
  const [index, setIndex] = useState(0)

  useEffect(() => {
    if (length <= 1) return

    const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
    if (reduceMotion) return

    let timer: number | undefined

    const start = () => {
      timer = window.setInterval(() => setIndex((i) => (i + 1) % length), intervalMs)
    }
    const stop = () => {
      if (timer) window.clearInterval(timer)
      timer = undefined
    }

    const onVisibility = () => (document.hidden ? stop() : start())

    start()
    document.addEventListener('visibilitychange', onVisibility)

    return () => {
      stop()
      document.removeEventListener('visibilitychange', onVisibility)
    }
  }, [length, intervalMs])

  return index
}
