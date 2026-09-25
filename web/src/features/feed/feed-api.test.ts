import { describe, expect, it, vi } from 'vitest'
import { formatWindow, timeAgo } from './feed-api'

/** Pin the clock so every assertion is about the function, not the wall time. */
function at(nowIso: string, run: () => void) {
  vi.useFakeTimers()
  vi.setSystemTime(new Date(nowIso))
  try {
    run()
  } finally {
    vi.useRealTimers()
  }
}

describe('timeAgo', () => {
  it('reads as a person would say it', () => {
    at('2026-09-15T12:00:00Z', () => {
      expect(timeAgo('2026-09-15T11:59:40Z')).toBe('20 seconds ago')
      expect(timeAgo('2026-09-15T11:58:00Z')).toBe('2 minutes ago')
      expect(timeAgo('2026-09-15T09:00:00Z')).toBe('3 hours ago')
      expect(timeAgo('2026-09-12T12:00:00Z')).toBe('3 days ago')
      expect(timeAgo('2026-08-25T12:00:00Z')).toBe('3 weeks ago')
    })
  })

  it('does not under-report by a unit - eight days is not "yesterday"', () => {
    // The original table paired each divisor with the unit below it, so every value was
    // labelled one unit too small. Three weeks showed on the feed as "3 days ago".
    at('2026-09-15T12:00:00Z', () => {
      expect(timeAgo('2026-09-07T12:00:00Z')).toBe('last week')
      expect(timeAgo('2026-09-14T12:00:00Z')).toBe('yesterday')
    })
  })

  it('never reports a future post as negative', () => {
    at('2026-09-15T12:00:00Z', () => {
      expect(timeAgo('2026-09-15T12:00:10Z')).not.toMatch(/-/)
    })
  })
})

describe('formatWindow', () => {
  it('shows the day once and both times, dropping :00 on whole hours', () => {
    const text = formatWindow('2026-09-14T07:00:00', '2026-09-14T09:30:00')
    expect(text).toMatch(/Sep 14/)
    expect(text).toMatch(/7 AM-9:30 AM$/)
  })
})
