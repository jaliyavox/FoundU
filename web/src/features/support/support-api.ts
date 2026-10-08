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
  /** Sent from the support assistant's draft - it was asked first and could not help. */
  viaAssistant?: boolean
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
  viaAssistant?: boolean
  /** On a create: the message joined a live ticket on the same topic instead of opening one. */
  addedToExisting?: boolean
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
  viaAssistant?: boolean
}) => api.post<TicketDetail>('/api/support/tickets', input)

/* ------------------------------------------------------------------ the assistant */

export interface AssistantTurn {
  role: 'user' | 'assistant'
  text: string
}

export interface TicketDraft {
  subject: string
  category: TicketCategory
  body: string
}

/**
 * answered - a help-guide entry fits; clarify - it asks for more; escalate - it cannot fix
 * this and `ticket` is a draft; unavailable - the AI is down and the draft is the person's
 * own words. A draft is never sent for them.
 */
export interface AssistantResponse {
  phase: 'answered' | 'clarify' | 'escalate' | 'unavailable'
  reply: string
  topic: string | null
  ticket: TicketDraft | null
}

export const askSupportAssistant = (message: string, history: AssistantTurn[], lastTopic: string | null) =>
  api.post<AssistantResponse>('/api/support/assistant', { message, history, lastTopic })

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

/** Spam, duplicates, tickets opened by mistake. Admin only - the API refuses staff. */
export const deleteTicket = (id: string) => api.delete<void>(`/api/admin/support/tickets/${id}`)

export const updateTicket = (id: string, input: { status: TicketStatus; assignedToUserId?: string | null }) =>
  api.put<TicketDetail>(`/api/admin/support/tickets/${id}`, input)
