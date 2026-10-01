import { api } from '@/lib/api/client'
import type { PagedResult } from '@/lib/api/types'

export const TICKET_CATEGORIES = [
  'Account',
  'LostReport',
  'FoundItem',
  'Claim',
  'Collection',
  'Technical',
  'Other',
] as const

export type TicketCategory = (typeof TICKET_CATEGORIES)[number]
export type TicketStatus = 'Open' | 'Waiting' | 'Resolved' | 'Closed'

/** What each category is called on screen - the enum names are for the wire, not for people. */
export const CATEGORY_LABELS: Record<TicketCategory, string> = {
  Account: 'My account',
  LostReport: 'A lost report',
  FoundItem: 'Something I found',
  Claim: 'A claim',
  Collection: 'Collecting an item',
  Technical: 'Something is broken',
  Other: 'Something else',
}

export const STATUS_LABELS: Record<TicketStatus, string> = {
  Open: 'With us',
  Waiting: 'Waiting on you',
  Resolved: 'Resolved',
  Closed: 'Closed',
}

export interface TicketMessage {
  id: string
  senderName: string
  isMine: boolean
  isStaffReply: boolean
  body: string
  createdAt: string
}

export interface TicketListItem {
  id: string
  subject: string
  category: TicketCategory
  status: TicketStatus
  raisedByName: string
  assignedToName: string | null
  messageCount: number
  /** Messages the reader has not seen - each side counts only the other side's. */
  unreadCount: number
  lastActivityAt: string
  createdAt: string
}

export interface TicketDetail {
  id: string
  subject: string
  category: TicketCategory
  status: TicketStatus
  raisedById: string
  raisedByName: string
  /** Staff only - null for the person who raised it. */
  raisedByEmail: string | null
  assignedToUserId: string | null
  assignedToName: string | null
  relatedEntityType: string | null
  relatedEntityId: string | null
  lastActivityAt: string
  resolvedAt: string | null
  createdAt: string
  messages: TicketMessage[]
}

export interface TicketQuery {
  page?: number
  pageSize?: number
  status?: string
  category?: string
  search?: string
  assigned?: 'mine' | 'none'
}

function toParams(query: TicketQuery) {
  const params = new URLSearchParams({
    page: String(query.page ?? 1),
    pageSize: String(query.pageSize ?? 20),
  })
  if (query.status) params.set('status', query.status)
  if (query.category) params.set('category', query.category)
  if (query.search?.trim()) params.set('search', query.search.trim())
  if (query.assigned) params.set('assigned', query.assigned)
  return params
}

export const createTicket = (input: {
  subject: string
  category: TicketCategory
  body: string
  relatedEntityType?: string
  relatedEntityId?: string
}) => api.post<TicketDetail>('/api/support/tickets', input)

export const getMyTickets = (query: TicketQuery = {}) =>
  api.get<PagedResult<TicketListItem>>(`/api/support/tickets?${toParams(query)}`)

export const getTicket = (id: string) => api.get<TicketDetail>(`/api/support/tickets/${id}`)

export const replyToTicket = (id: string, body: string) =>
  api.post<TicketDetail>(`/api/support/tickets/${id}/messages`, { body })

/* ------------------------------------------------------------------ the desk */

export const getSupportQueue = (query: TicketQuery = {}) =>
  api.get<PagedResult<TicketListItem>>(`/api/admin/support/tickets?${toParams(query)}`)

export interface SupportQueueStats {
  open: number
  waiting: number
  resolvedToday: number
  unassigned: number
  oldestOpenHours: number
}

export const getSupportStats = () => api.get<SupportQueueStats>('/api/admin/support/stats')

export const updateTicket = (id: string, input: { status: TicketStatus; assignedToUserId?: string | null }) =>
  api.put<TicketDetail>(`/api/admin/support/tickets/${id}`, input)
