import { api } from '@/lib/api/client'
import type { PagedResult } from '@/lib/api/types'

/** Mirrors FoundU.Application.LostReports.Dtos.LostReportFeedItemDto. */
export interface LostReportFeedItem {
  id: string
  postedByName: string
  /** Server-computed: true when the signed-in caller posted this. */
  isMine: boolean
  categoryName: string
  itemTypeName: string
  lastSeenLocationName: string
  description: string
  primaryColor: string | null
  estimatedLostFromAt: string
  estimatedLostToAt: string
  photoUrls: string[]
  createdAt: string
}

export interface FeedQuery {
  page: number
  pageSize: number
  search?: string
}

/**
 * Public feed. Readable without an account, but the token goes along when there is one so
 * the API can mark the caller's own posts - see `optionalAuth`.
 */
export function getFeed({ page, pageSize, search }: FeedQuery) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search?.trim()) params.set('search', search.trim())

  return api.get<PagedResult<LostReportFeedItem>>(`/api/lost-reports/feed?${params}`, {
    optionalAuth: true,
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

/* --------------------------------------------------------------- found claims */

export interface LostReportFoundClaim {
  reportId: string
  totalFinders: number
  createdAt: string
}

/**
 * "I found this". Recorded against the report so the author sees it immediately, whether or
 * not the finder goes on to write a message. Pressing it twice records one claim.
 */
export const registerFoundClaim = (reportId: string) =>
  api.post<LostReportFoundClaim>(`/api/lost-reports/${reportId}/found-claims`)

/* ------------------------------------------------------------------ messages */

export interface LostReportMessage {
  id: string
  senderName: string
  body: string
  isRead: boolean
  createdAt: string
}

/** Authenticated - this is the point of the sign-in gate on the feed. */
export const sendMessage = (reportId: string, body: string) =>
  api.post<LostReportMessage>(`/api/lost-reports/${reportId}/messages`, { body })
