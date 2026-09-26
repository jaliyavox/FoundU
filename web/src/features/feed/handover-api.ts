import { api } from '@/lib/api/client'

/** Mirrors FoundU.Application.Handovers.Dtos.HandoverDto. */
export interface Handover {
  reportId: string
  status: 'Declared' | 'AwaitingHandIn' | 'InCustody' | 'Collected' | 'Expired' | 'Cancelled'
  /** Only ever filled for the finder who started it and for the report's owner. */
  code: string | null
  startedAt: string | null
  expiresAt: string | null
  handedInAt: string | null
  collectedAt: string | null
  storageLocationName: string | null
  finderName: string | null
}

/** "I will take it to security." Pressing it twice returns the same handover. */
export const startHandover = (reportId: string) =>
  api.post<Handover>(`/api/lost-reports/${reportId}/handover`)

export const cancelHandover = (reportId: string) =>
  api.post<Handover>(`/api/lost-reports/${reportId}/handover/cancel`)

/**
 * Null when there is nothing in flight for this reader - which is the common case, since
 * most reports have no handover.
 *
 * The `?? null` matters: the API answers a bare `null` body, which arrives here as undefined,
 * and TanStack Query treats an undefined result as a bug and puts the query into error.
 */
export const getHandover = (reportId: string) =>
  api.get<Handover | null>(`/api/lost-reports/${reportId}/handover`).then(value => value ?? null)

/* --------------------------------------------------------------- the desk */

export interface HandoverLookup {
  reportId: string
  status: Handover['status']
  itemTypeName: string
  categoryName: string
  description: string
  primaryColor: string | null
  lastSeenLocationName: string
  ownerName: string
  ownerStudentNumber: string | null
  finderName: string
  startedAt: string
  expiresAt: string | null
  handedInAt: string | null
  storageLocationName: string | null
  /** "receive" - the finder is here with it; "release" - the owner is here for it. */
  nextStep: 'receive' | 'release' | 'done'
}

export const lookupHandover = (code: string) =>
  api.get<HandoverLookup>(`/api/handovers/by-code/${code.replace(/\s/g, '')}`)

export const receiveHandover = (code: string, storageLocationId: string, note?: string) =>
  api.post<HandoverLookup>(`/api/handovers/by-code/${code.replace(/\s/g, '')}/receive`, {
    storageLocationId,
    note,
  })

export const releaseHandover = (code: string, note?: string) =>
  api.post<HandoverLookup>(`/api/handovers/by-code/${code.replace(/\s/g, '')}/release`, {
    ownerIdChecked: true,
    note,
  })
