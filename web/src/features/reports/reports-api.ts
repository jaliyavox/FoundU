import { api } from '@/lib/api/client'
import type { PagedResult } from '@/lib/api/types'

/* ---------------------------------------------------------------- reference */

export interface ItemType {
  id: string
  categoryId: string
  name: string
}

export interface Category {
  id: string
  name: string
  description: string | null
  itemTypes: ItemType[]
}

export interface CampusLocation {
  id: string
  name: string
  building: string | null
  description: string | null
}

export interface StorageLocation {
  id: string
  name: string
  building: string | null
  capacity: number | null
}

export const getCategories = () => api.get<Category[]>('/api/reference/categories')
export const getLocations = () => api.get<CampusLocation[]>('/api/reference/locations')
export const getStorageLocations = () => api.get<StorageLocation[]>('/api/reference/storage-locations')

/* -------------------------------------------------------------- lost reports */

export interface CreateLostReportInput {
  categoryId: string
  itemTypeId: string
  lastSeenLocationId: string
  description: string
  primaryColor?: string
  secondaryColor?: string
  estimatedLostFromAt: string
  estimatedLostToAt: string
}

export interface LostReportListItem {
  id: string
  categoryName: string
  itemTypeName: string
  lastSeenLocationName: string
  description: string
  primaryColor: string | null
  estimatedLostFromAt: string
  estimatedLostToAt: string
  status: string
  createdAt: string
}

/** Mirrors FoundU.Application.Common.PhotoRules - kept in step by hand, and by the API. */
export const PHOTO_RULES = {
  maxPhotos: 2,
  maxBytes: 5 * 1024 * 1024,
  maxSizeLabel: '5 MB',
  accept: 'image/jpeg,image/png,image/webp',
} as const

/**
 * Photos are attached after the report exists, because the API keys them to its id. A failure
 * here leaves the report in place - the caller decides how loudly to complain.
 */
export async function uploadLostReportPhotos(reportId: string, files: File[]) {
  const body = new FormData()
  files.forEach((file) => body.append('photos', file))

  const response = await fetch(
    `${import.meta.env.VITE_API_BASE_URL}/api/lost-reports/${reportId}/photos`,
    {
      method: 'POST',
      // FormData sets its own multipart boundary; setting Content-Type here breaks it.
      headers: { Authorization: `Bearer ${localStorage.getItem('foundu.accessToken') ?? ''}` },
      body,
    },
  )

  if (!response.ok) {
    const problem = await response.json().catch(() => ({}))
    throw new Error(problem.detail ?? 'The photos could not be uploaded.')
  }

  return (await response.json()) as { id: string; url: string }[]
}

export const createLostReport = (input: CreateLostReportInput) =>
  api.post<{ id: string }>('/api/lost-reports', input)

export const getMyLostReports = (page: number, pageSize: number) =>
  api.get<PagedResult<LostReportListItem>>(`/api/lost-reports/mine?page=${page}&pageSize=${pageSize}`)

export const withdrawLostReport = (id: string, reason?: string) =>
  api.post<unknown>(`/api/lost-reports/${id}/withdraw`, { reason })

/* ------------------------------------------------------------- found reports */

export interface CreateFoundReportInput {
  categoryId: string
  itemTypeId: string
  foundLocationId: string
  storageLocationId: string
  generalDescription: string
  privateVerificationDetails?: string
  primaryColor?: string
  secondaryColor?: string
  foundAt: string
}

export interface FoundReportListItem {
  id: string
  categoryName: string
  itemTypeName: string
  foundLocationName: string
  storageLocationName: string
  generalDescription: string
  primaryColor: string | null
  foundAt: string
  status: string
  hasVerificationDetails: boolean
  createdAt: string
}

export interface FoundReportQuery {
  page: number
  pageSize: number
  search?: string
  status?: string
}

export const createFoundReport = (input: CreateFoundReportInput) =>
  api.post<{ id: string }>('/api/found-reports', input)

export const getFoundReports = ({ page, pageSize, search, status }: FoundReportQuery) => {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search?.trim()) params.set('search', search.trim())
  if (status) params.set('status', status)
  return api.get<PagedResult<FoundReportListItem>>(`/api/found-reports?${params}`)
}

/* -------------------------------------------------------------------- shared */

/** A <input type="datetime-local"> value is local wall time; the API wants UTC ISO. */
export const toUtcIso = (localValue: string) => new Date(localValue).toISOString()

/** Default the "lost between" window to the last couple of hours. */
export function defaultWindow() {
  const now = new Date()
  const twoHoursAgo = new Date(now.getTime() - 2 * 60 * 60 * 1000)
  const toLocalInput = (date: Date) =>
    new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
  return { from: toLocalInput(twoHoursAgo), to: toLocalInput(now) }
}

export const formatDateTime = (iso: string) =>
  new Date(iso).toLocaleString('en', {
    day: 'numeric',
    month: 'short',
    hour: 'numeric',
    minute: '2-digit',
  })
