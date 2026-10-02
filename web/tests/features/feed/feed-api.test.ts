import { describe, expect, it, vi } from 'vitest'
import { formatWindow, postStage, timeAgo } from '../../../src/features/feed/feed-api'

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

describe('postStage', () => {
  const post = { isMine: false, handedToSecurityAt: null }

  it('says where a found post has got to, from the finder to the owner collecting it', () => {
    expect(postStage({ ...post, status: 'Posted' }).badge).toBe('Not at a desk yet')
    expect(postStage({ ...post, status: 'Unclaimed' }).badge).toBe('At the security desk')
    expect(postStage({ ...post, status: 'Claimed' }).badge).toBe('Owner on the way')
  })

  it('tells the finder their own post apart, and that they handed it in', () => {
    expect(postStage({ ...post, isMine: true, status: 'Posted' }).badge).toBe('Your post')
    expect(postStage({ ...post, isMine: true, status: 'Posted', handedToSecurityAt: '2026-10-02T08:00:00Z' }).badge)
      .toBe('Handed to security')
    // Once the desk confirms it, the desk's word replaces the finder's.
    expect(postStage({ ...post, isMine: true, status: 'Unclaimed', handedToSecurityAt: '2026-10-02T08:00:00Z' }).badge)
      .toBe('At the security desk')
  })
})
