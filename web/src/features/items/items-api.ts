import { api } from '@/lib/api/client'
import type { PagedResult } from '@/lib/api/types'

/** Mirrors FoundU.Application.FoundReports.Dtos.FoundReportListItemDto. */
export interface FoundReportListItem {
  id: string
  categoryName: string
  itemTypeName: string
  foundLocationName: string
  storageLocationName: string
  generalDescription: string
  primaryColor: string | null
  foundAt: string
  status: FoundReportStatus
  /** Whether staff recorded a hidden detail to verify ownership with. Not the detail itself. */
  hasVerificationDetails: boolean
  createdAt: string
}

/** Staff/Admin only - this is the shape that carries the hidden ownership evidence. */
export interface FoundReportDetail {
  id: string
  categoryId: string
  categoryName: string
  itemTypeId: string
  itemTypeName: string
  foundLocationId: string
  foundLocationName: string
  storageLocationId: string
  storageLocationName: string
  generalDescription: string
  privateVerificationDetails: string | null
  primaryColor: string | null
  secondaryColor: string | null
  foundAt: string
  status: FoundReportStatus
  staffId: string
  staffName: string
  createdAt: string
  updatedAt: string
}

export type FoundReportStatus = 'Unclaimed' | 'Claimed' | 'Returned' | 'Disposed'

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

export interface ItemsQuery {
  page: number
  pageSize: number
  search?: string
  status?: string
  categoryId?: string
}

export function getItems({ page, pageSize, search, status, categoryId }: ItemsQuery) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search?.trim()) params.set('search', search.trim())
  if (status) params.set('status', status)
  if (categoryId) params.set('categoryId', categoryId)
  return api.get<PagedResult<FoundReportListItem>>(`/api/found-reports?${params}`)
}

export const getItem = (id: string) => api.get<FoundReportDetail>(`/api/found-reports/${id}`)

export const createItem = (input: CreateFoundReportInput) =>
  api.post<FoundReportDetail>('/api/found-reports', input)

/** The staff view across every student's lost reports - what an item gets linked to. */
export const searchLostReports = (page: number, pageSize: number, search?: string) => {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    status: 'Active',
  })
  if (search?.trim()) params.set('search', search.trim())
  return api.get<PagedResult<LostReportRow>>(`/api/lost-reports?${params}`)
}

/** Mirrors LostReportListItemDto - the fields the picker needs to tell two reports apart. */
export interface LostReportRow {
  id: string
  categoryName: string
  itemTypeName: string
  lastSeenLocationName: string
  description: string
  primaryColor: string | null
  estimatedLostFromAt: string
  estimatedLostToAt: string
  status: string
  photoUrls: string[]
  createdAt: string
}

/** How each status reads on the desk, and what it says about the item's whereabouts. */
export const ITEM_STATUS_STYLES: Record<FoundReportStatus, string> = {
  Unclaimed: 'border-brand-green/35 bg-brand-green/12 text-brand-forest dark:text-brand-sage',
  Claimed: 'border-amber-500/40 bg-amber-500/12 text-amber-700 dark:text-amber-300',
  Returned: 'border-foreground/12 bg-foreground/5 text-muted-foreground',
  Disposed: 'border-foreground/12 bg-transparent text-muted-foreground',
}
