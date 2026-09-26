import { api } from '@/lib/api/client'

/** Mirrors FoundU.Application.Honor.Dtos.HelpToFindActivityDto. */
export interface HelpActivity {
  /** "found-claim" - they said they found someone's item; "found-post" - they posted one they picked up. */
  kind: 'found-claim' | 'found-post'
  reportId: string
  itemTypeName: string
  locationName: string
  status: string
  /** The six digits to quote at the desk, while the item still needs walking there. */
  handInCode: string | null
  ownerName: string | null
  pointsEarned: number
  createdAt: string
}

export interface HelpToFind {
  honorPoints: number
  itemsReturned: number
  handIns: number
  openHelpOffers: number
  activity: HelpActivity[]
}

export const getHelpToFind = () => api.get<HelpToFind>('/api/help-to-find')
