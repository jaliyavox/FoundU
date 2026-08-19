import { api } from '@/lib/api/client'
import type { PagedResult } from '@/lib/api/types'

/** Mirrors FoundU.Application.Admin.Dtos.AdminUserListItemDto. */
export interface AdminUser {
  id: string
  fullName: string
  email: string
  role: 'Student' | 'Staff' | 'Admin'
  studentNumber: string | null
  isSuspended: boolean
  suspensionReason: string | null
  suspendedAt: string | null
  suspendedByName: string | null
  lostReportCount: number
  createdAt: string
}

/** Mirrors FoundU.Application.Admin.Dtos.AdminUserStatsDto. */
export interface AdminUserStats {
  totalUsers: number
  students: number
  staff: number
  admins: number
  suspended: number
  joinedLast30Days: number
}

export const getUserStats = () => api.get<AdminUserStats>('/api/admin/users/stats')

export interface AdminUserQuery {
  page: number
  pageSize: number
  search?: string
  role?: string
  isSuspended?: boolean
}

export function getUsers({ page, pageSize, search, role, isSuspended }: AdminUserQuery) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search?.trim()) params.set('search', search.trim())
  if (role) params.set('role', role)
  if (isSuspended !== undefined) params.set('isSuspended', String(isSuspended))

  return api.get<PagedResult<AdminUser>>(`/api/admin/users?${params}`)
}

export const suspendUser = (id: string, reason: string) =>
  api.post<AdminUser>(`/api/admin/users/${id}/suspend`, { reason })

export const reinstateUser = (id: string) => api.post<AdminUser>(`/api/admin/users/${id}/reinstate`)

export const formatDate = (iso: string) =>
  new Date(iso).toLocaleDateString('en', { day: 'numeric', month: 'short', year: 'numeric' })
