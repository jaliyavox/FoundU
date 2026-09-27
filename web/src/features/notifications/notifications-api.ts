import { api } from '@/lib/api/client'
import type { PagedResult } from '@/lib/api/types'

/** Mirrors FoundU.Application.Notifications.Dtos.NotificationDto. */
export interface AppNotification {
  id: string
  type: NotificationType
  title: string
  message: string
  isRead: boolean
  /** With the id below, this is what the row links to. */
  relatedEntityType: string | null
  relatedEntityId: string | null
  createdAt: string
}

export type NotificationType =
  | 'PossibleMatchFound'
  | 'VerificationQuestionAvailable'
  | 'RevisionRequested'
  | 'ClaimApproved'
  | 'ClaimRejected'
  | 'CollectionInstructions'
  | 'ItemReportedFound'
  | 'MessageReceived'
  | 'FoundPostConfirmed'
  | 'FoundPostRecognised'
  | 'ItemReturnedToOwner'
  | 'SupportTicketReply'
  | 'SupportTicketUpdated'
  | 'HandoverStarted'
  | 'HandoverCancelled'

export const getNotifications = (page = 1, pageSize = 15) =>
  api.get<PagedResult<AppNotification>>(`/api/notifications?page=${page}&pageSize=${pageSize}`)

export const getUnreadCount = () => api.get<{ unread: number }>('/api/notifications/unread-count')

export const markRead = (id: string) => api.post<AppNotification>(`/api/notifications/${id}/read`)

export const markAllRead = () => api.post<{ unread: number }>('/api/notifications/read-all')

/**
 * Where a notification takes you.
 *
 * A claim opens its own screen; the two report-level events open My reports, where the
 * suggestion panel and the report cards live. Anything unrecognised stays inert rather than
 * guessing at a route that may not exist.
 */
export function linkFor(notification: AppNotification): string | null {
  if (!notification.relatedEntityId) return null

  // These go to the finder but point at the owner's report, which the finder cannot open.
  if (notification.type === 'FoundPostConfirmed' || notification.type === 'ItemReturnedToOwner') return '/feed'

  switch (notification.relatedEntityType) {
    case 'SupportTicket':
      return '/support'
    case 'Claim':
      return `/claims/${notification.relatedEntityId}`
    case 'FoundReport':
      // A finder's own post: confirmed at a desk, or recognised by its owner.
      return '/feed'
    case 'MatchSuggestion':
    case 'LostReport':
      // The author's reply reaches a finder, whose side of the thread lives on the feed post,
      // not on a report they do not own.
      return notification.type === 'MessageReceived' && notification.title.startsWith('A reply') ? '/feed' : '/my-reports'
    default:
      return null
  }
}
