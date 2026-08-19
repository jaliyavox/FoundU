import { api } from '@/lib/api/client'
import type { PagedResult } from '@/lib/api/types'

/** Mirrors FoundU.Application.LostReports.Dtos.LostReportFeedItemDto. */
export interface LostReportFeedItem {
  id: string
  postedByName: string
  categoryName: string
  itemTypeName: string
  lastSeenLocationName: string
  description: string
  primaryColor: string | null
  estimatedLostFromAt: string
  estimatedLostToAt: string
  createdAt: string
}

export interface FeedQuery {
  page: number
  pageSize: number
  search?: string
}

/**
 * Public feed. `anonymous: true` skips the bearer token and the 401-refresh dance - this
 * endpoint is deliberately readable without an account.
 */
export function getFeed({ page, pageSize, search }: FeedQuery) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search?.trim()) params.set('search', search.trim())

  return api.get<PagedResult<LostReportFeedItem>>(`/api/lost-reports/feed?${params}`, {
    anonymous: true,
  })
}

/** "3 hours ago", "2 days ago" - good enough for a feed, no date library needed. */
export function timeAgo(iso: string) {
  const seconds = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000)

  const units: [number, Intl.RelativeTimeFormatUnit][] = [
    [60, 'second'],
    [60, 'minute'],
    [24, 'hour'],
    [7, 'day'],
    [4.35, 'week'],
    [12, 'month'],
  ]

  let value = seconds
  let unit: Intl.RelativeTimeFormatUnit = 'second'

  for (const [size, nextUnit] of units) {
    if (value < size) break
    value /= size
    unit = nextUnit
  }

  return new Intl.RelativeTimeFormat('en', { numeric: 'auto' }).format(-Math.round(value), unit)
}

/** "Tue 2-4pm" - the window the student thinks they lost it in. */
export function formatWindow(fromIso: string, toIso: string) {
  const from = new Date(fromIso)
  const to = new Date(toIso)

  const day = from.toLocaleDateString('en', { weekday: 'short', day: 'numeric', month: 'short' })
  const time = (date: Date) =>
    date.toLocaleTimeString('en', { hour: 'numeric', minute: '2-digit' }).replace(':00', '')

  return `${day}, ${time(from)}-${time(to)}`
}
