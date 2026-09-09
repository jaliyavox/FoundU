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

  switch (notification.relatedEntityType) {
    case 'Claim':
      return `/claims/${notification.relatedEntityId}`
    case 'MatchSuggestion':
    case 'LostReport':
      return '/my-reports'
    default:
      return null
  }
}
