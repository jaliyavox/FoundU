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
  /** Set when a student posted it rather than a desk logging it. */
  finderName: string | null
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
  /** Null while it is only a finder's post. */
  storageLocationId: string | null
  storageLocationName: string | null
  generalDescription: string
  privateVerificationDetails: string | null
  primaryColor: string | null
  secondaryColor: string | null
  foundAt: string
  status: FoundReportStatus
  staffId: string | null
  staffName: string | null
  finderName: string | null
  /** The finder's code - staff only, and only while it is a post. */
  handInCode: string | null
  createdAt: string
  updatedAt: string
}

export type FoundReportStatus = 'Posted' | 'Unclaimed' | 'Claimed' | 'Returned' | 'Disposed'

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
  /** The six digits the finder quoted. Links the item to that report the moment it is logged. */
  handInCode?: string
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
  Posted: 'border-amber-500/40 bg-amber-500/12 text-amber-700 dark:text-amber-300',
  Unclaimed: 'border-brand-green/35 bg-brand-green/12 text-brand-forest dark:text-brand-sage',
  Claimed: 'border-amber-500/40 bg-amber-500/12 text-amber-700 dark:text-amber-300',
  Returned: 'border-foreground/12 bg-foreground/5 text-muted-foreground',
  Disposed: 'border-foreground/12 bg-transparent text-muted-foreground',
}

/** The desk turning a finder's post into a real record. */
export const confirmFoundPost = (id: string, input: { storageLocationId: string; privateVerificationDetails?: string; generalDescription?: string }) =>
  api.post<FoundReportDetail>(`/api/found-posts/${id}/confirm`, input)

/** Staff pulling a post up by the six digits the finder quotes. 404 if none. */
export const getFoundPostByCode = (code: string) => api.get<FoundReportDetail>(`/api/found-posts/by-code/${code}`)

export const ITEM_STATUS_LABELS: Record<FoundReportStatus, string> = {
  Posted: 'Not at a desk yet',
  Unclaimed: 'In storage',
  Claimed: 'Claimed',
  Returned: 'Returned',
  Disposed: 'Disposed',
}
