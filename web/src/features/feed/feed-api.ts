import type { QueryClient } from '@tanstack/react-query'
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
/** One report as the feed shows it - for a link straight to it. 404 once it is off the feed. */
export const getFeedItem = (id: string) => api.get<LostReportFeedItem>(`/api/lost-reports/feed/${id}`)

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
  canMessageFinder?: boolean
  storageLocationName?: string | null
  categoryName: string
  itemTypeName: string
  foundLocationName: string
  description: string
  primaryColor: string | null
  foundAt: string
  status: 'Posted' | 'Unclaimed' | 'Claimed' | 'Returned' | 'Disposed'
  /** Only on your own post - what you quote at the desk. */
  handInCode: string | null
  handedToSecurityAt: string | null
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

export const declareFoundPostHandedIn = (id: string) =>
  api.post<FoundPostItem>(`/api/found-posts/${id}/hand-in`)

export const getMyFoundPosts = (page = 1, pageSize = 20) =>
  api.get<PagedResult<FoundPostItem>>(`/api/found-posts/mine?page=${page}&pageSize=${pageSize}`)

/**
 * Messages about a found post - the same shape as a lost-report thread, with the roles the
 * other way round: the finder is the one being written to.
 */
export type FoundPostMessage = LostReportMessage

/** An enquirer leaves recipientId empty; the finder names the enquirer they are answering. */
export const sendFoundPostMessage = (postId: string, body: string, recipientId?: string) =>
  api.post<FoundPostMessage>(`/api/found-posts/${postId}/messages`, { body, recipientId })

/** 403 for somebody who is in none of the threads - that is an empty thread, not a failure. */
export const getFoundPostMessages = (postId: string) =>
  api.get<FoundPostMessage[]>(`/api/found-posts/${postId}/messages`)

/**
 * A found post shows in three lists: the Found board, the Fresh finds strip on the lost feed,
 * and the finder's own My reports. Anything that changes a post refreshes all three.
 */
export const invalidateFoundPosts = (queryClient: QueryClient) =>
  Promise.all(
    ['found-feed', 'found-strip', 'my-found-posts'].map(key => queryClient.invalidateQueries({ queryKey: [key] })),
  )

/**
 * Where a found post has got to. A post stays on the board until its owner has the item
 * back, so the board says which of three places it is: still with the finder, in storage at
 * a security desk, or spoken for and waiting to be collected.
 */
export function postStage(item: Pick<FoundPostItem, 'status' | 'isMine' | 'handedToSecurityAt'>): {
  badge: string
  note: string
  tone: 'amber' | 'green' | 'sky'
} {
  if (item.status === 'Unclaimed') {
    return {
      badge: 'At the security desk',
      note: 'In storage at a security desk. Think it is yours? Link it to your report below and claim it - staff check before handing it over.',
      tone: 'green',
    }
  }
  if (item.status === 'Claimed') {
    return {
      badge: 'Owner on the way',
      note: 'Its owner has proved it is theirs and is collecting it from security.',
      tone: 'sky',
    }
  }
  if (item.isMine && item.handedToSecurityAt) {
    return {
      badge: 'Handed to security',
      note: 'You marked this as handed to security. It can be claimed after security confirms receipt.',
      tone: 'amber',
    }
  }
  return {
    badge: item.isMine ? 'Your post' : 'Not at a desk yet',
    note: 'Not at a desk yet. It can be claimed once the finder hands it in.',
    tone: 'amber',
  }
}

/** Badge colours on the dark board cards. */
export const STAGE_BADGE: Record<'amber' | 'green' | 'sky', string> = {
  amber: 'bg-amber-400/90 text-neutral-900',
  green: 'bg-brand-sage text-neutral-900',
  sky: 'bg-sky-300 text-neutral-900',
}

/** Notes on the light detail sheet. */
export const STAGE_NOTE: Record<'amber' | 'green' | 'sky', string> = {
  amber: 'border-amber-500/30 bg-amber-50 text-amber-900',
  green: 'border-brand-green/30 bg-brand-green/10 text-brand-forest',
  sky: 'border-sky-500/30 bg-sky-50 text-sky-900',
}
