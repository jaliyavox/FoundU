import { api } from '@/lib/api/client'

/** The route segment for each kind of reference data. */
export type ReferenceKind = 'categories' | 'item-types' | 'locations' | 'storage'

/**
 * `usageCount` is how many reports and items name this row, and it decides what may happen to
 * it: anything in use can be retired - hidden from the pickers, still readable on the records
 * that name it - but only something unused can be deleted.
 */
export interface ItemTypeAdmin {
  id: string
  categoryId: string
  name: string
  isActive: boolean
  usageCount: number
}

export interface CategoryAdmin {
  id: string
  name: string
  description: string | null
  isHighlighted: boolean
  isActive: boolean
  usageCount: number
  itemTypes: ItemTypeAdmin[]
}

export interface LocationAdmin {
  id: string
  name: string
  building: string | null
  description: string | null
  isActive: boolean
  usageCount: number
}

export interface StorageAdmin {
  id: string
  name: string
  building: string | null
  capacity: number | null
  isActive: boolean
  usageCount: number
}

export interface ReferenceAdmin {
  categories: CategoryAdmin[]
  locations: LocationAdmin[]
  storage: StorageAdmin[]
}

/** One shape for every kind; fields that do not apply to a kind are ignored for it. */
export interface ReferenceItemInput {
  name: string
  description?: string | null
  building?: string | null
  capacity?: number | null
  categoryId?: string | null
  isHighlighted?: boolean | null
}

export const getReferenceAdmin = () => api.get<ReferenceAdmin>('/api/admin/reference')

export const createReference = (kind: ReferenceKind, input: ReferenceItemInput) =>
  api.post<{ id: string }>(`/api/admin/reference/${kind}`, input)

export const updateReference = (kind: ReferenceKind, id: string, input: ReferenceItemInput) =>
  api.put<void>(`/api/admin/reference/${kind}/${id}`, input)

/** Retire (false) or restore (true). */
export const setReferenceActive = (kind: ReferenceKind, id: string, isActive: boolean) =>
  api.post<void>(`/api/admin/reference/${kind}/${id}/active`, { isActive })

export const deleteReference = (kind: ReferenceKind, id: string) =>
  api.delete<void>(`/api/admin/reference/${kind}/${id}`)
