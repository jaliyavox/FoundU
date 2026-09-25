import { useCallback, useEffect, useState } from 'react'

export type Theme = 'dark' | 'light'

const STORAGE_KEY = 'foundu.dashboardTheme'

/**
 * Theme for the signed-in dashboard, dark by default.
 *
 * Deliberately scoped: the class is removed when the dashboard unmounts, so the public
 * landing and feed pages keep their own light/dark banding instead of being flipped by a
 * preference set in here.
 */
export function useDashboardTheme() {
  const [theme, setTheme] = useState<Theme>(() => {
    const stored = localStorage.getItem(STORAGE_KEY)
    return stored === 'light' || stored === 'dark' ? stored : 'dark'
  })

  useEffect(() => {
    const root = document.documentElement
    root.classList.toggle('dark', theme === 'dark')
    localStorage.setItem(STORAGE_KEY, theme)

    return () => root.classList.remove('dark')
  }, [theme])

  const toggle = useCallback(() => {
    setTheme((current) => (current === 'dark' ? 'light' : 'dark'))
  }, [])

  return { theme, toggle }
}
