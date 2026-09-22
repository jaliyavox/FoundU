import { api } from '@/lib/api/client'
import type { PagedResult } from '@/lib/api/types'

/** Mirrors FoundU.Application.LostReports.Dtos.LostReportFeedItemDto. */
export interface LostReportFeedItem {
  id: string
  /** Six digits a finder quotes at the desk. Routing, not proof - which is why it is public. */
  handInCode: string
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

  // Each row is "divide by this to land in that unit". Pairing the divisor with the unit
  // below it - [60, 'second'] - labelled every value one unit too small, so eight days read
  // as "yesterday". The test in feed-api.test.ts pins this.
  const units: [number, Intl.RelativeTimeFormatUnit][] = [
    [60, 'minute'],
    [60, 'hour'],
    [24, 'day'],
    [7, 'week'],
    [4.35, 'month'],
    [12, 'year'],
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
  /** True when the reader wrote it - the thread lays out as a conversation. */
  isMine: boolean
  /** The other person in this thread; what the author passes back as recipientId to reply. */
  counterpartId: string
  counterpartName: string
  body: string
  isRead: boolean
  createdAt: string
}

/**
 * Authenticated - this is the point of the sign-in gate on the feed. A finder leaves
 * recipientId empty; the author names the finder they are replying to.
 */
export const sendMessage = (reportId: string, body: string, recipientId?: string) =>
  api.post<LostReportMessage>(`/api/lost-reports/${reportId}/messages`, { body, recipientId })

/** The reader's threads on a report. 403 for someone who is in none of them. */
export const getMessages = (reportId: string) => api.get<LostReportMessage[]>(`/api/lost-reports/${reportId}/messages`)

/** "483921" reads as "483 921" on screen. */
export const displayCode = (code: string) => (code.length === 6 ? `${code.slice(0, 3)} ${code.slice(3)}` : code)

/* --------------------------------------------------------------- found posts */

/** Mirrors FoundPostFeedItemDto - a finder's post before it reaches a desk. */
export interface FoundPostItem {
  id: string
  postedByName: string
  isMine: boolean
  categoryName: string
  itemTypeName: string
  foundLocationName: string
  description: string
  primaryColor: string | null
  foundAt: string
  status: 'Posted' | 'Unclaimed' | 'Claimed' | 'Returned' | 'Disposed'
  /** Only on your own post - what you quote at the desk. */
  handInCode: string | null
  createdAt: string
}

export function getFoundFeed({ page, pageSize, search, categoryId }: FeedQuery & { categoryId?: string | null }) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search?.trim()) params.set('search', search.trim())
  if (categoryId) params.set('categoryId', categoryId)
  return api.get<PagedResult<FoundPostItem>>(`/api/found-posts/feed?${params}`, { optionalAuth: true })
}

export interface CreateFoundPostInput {
  categoryId: string
  itemTypeId: string
  foundLocationId: string
  description: string
  primaryColor?: string
  foundAt: string
  lostReportHandInCode?: string
}

export const postFound = (input: CreateFoundPostInput) => api.post<FoundPostItem>('/api/found-posts', input)

/** "That is mine" - names one of the caller's own reports. The finder is told to hand it in. */
export const recogniseFoundPost = (id: string, lostReportId: string) =>
  api.post<FoundPostItem>(`/api/found-posts/${id}/recognise`, { lostReportId })

export const withdrawFoundPost = (id: string, reason?: string) =>
  api.post<FoundPostItem>(`/api/found-posts/${id}/withdraw`, { reason })

export const getMyFoundPosts = (page = 1, pageSize = 20) =>
  api.get<PagedResult<FoundPostItem>>(`/api/found-posts/mine?page=${page}&pageSize=${pageSize}`)
