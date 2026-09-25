import { describe, expect, it } from 'vitest'
import { linkFor, type AppNotification } from './notifications-api'

const base: AppNotification = {
  id: 'n1',
  type: 'ClaimApproved',
  title: '',
  message: '',
  isRead: false,
  relatedEntityType: null,
  relatedEntityId: null,
  createdAt: '2026-09-15T00:00:00Z',
}

describe('linkFor', () => {
  it('opens a claim on its own screen', () => {
    expect(linkFor({ ...base, relatedEntityType: 'Claim', relatedEntityId: 'c1' })).toBe('/claims/c1')
  })

  it('sends report-level events to My reports, where the suggestion panel and cards live', () => {
    expect(linkFor({ ...base, relatedEntityType: 'MatchSuggestion', relatedEntityId: 's1' })).toBe('/my-reports')
    expect(linkFor({ ...base, relatedEntityType: 'LostReport', relatedEntityId: 'r1' })).toBe('/my-reports')
  })

  it('stays inert rather than guessing a route for an unknown entity', () => {
    expect(linkFor({ ...base, relatedEntityType: 'AgentRun', relatedEntityId: 'a1' })).toBeNull()
  })

  it('stays inert when there is nothing to link to', () => {
    expect(linkFor({ ...base, relatedEntityType: 'Claim', relatedEntityId: null })).toBeNull()
  })
})
