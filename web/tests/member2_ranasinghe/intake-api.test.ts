import { describe, expect, it } from 'vitest'
import { isFinder, readIntakeHandoff, slotsFor, type IntakeResponse } from '../../src/features/intake/intake-api'

const response = (overrides: Partial<IntakeResponse> = {}): IntakeResponse => ({
  phase: 'no_match',
  reply: 'Nobody has reported a blue water bottle lost yet.',
  slots: { ...slotsFor('found'), itemType: 'Water Bottle', colour: 'blue' },
  draft: {
    categoryId: 'c1',
    itemTypeId: 't1',
    locationId: 'l1',
    description: 'Blue water bottle, found at Cafeteria.',
    primaryColor: 'blue',
    when: null,
  },
  match: null,
  ...overrides,
})

describe('the Ask FoundU hand-off', () => {
  it("carries a finder's draft for the same account", () => {
    const handoff = readIntakeHandoff({ intake: { ownerId: 'me', response: response() } }, 'me')
    expect(handoff?.response.draft.description).toBe('Blue water bottle, found at Cafeteria.')
    expect(isFinder(handoff!.response)).toBe(true)
  })

  it("never restores another account's conversation", () => {
    expect(readIntakeHandoff({ intake: { ownerId: 'someone-else', response: response() } }, 'me')).toBeNull()
  })

  it('accepts a lost report shown to a finder, and refuses a side it does not know', () => {
    const match = { id: 'r1', kind: 'lost' as const, itemType: 'Water Bottle', colour: 'blue', location: 'Cafeteria', description: 'Dented' }
    expect(readIntakeHandoff({ intake: { ownerId: 'me', response: response({ phase: 'matched', match }) } }, 'me')).not.toBeNull()

    const stolen = response({ slots: { ...response().slots, intent: 'stolen' as never } })
    expect(readIntakeHandoff({ intake: { ownerId: 'me', response: stolen } }, 'me')).toBeNull()
  })

  it('reads an answer with no side as an owner, as before', () => {
    const owner = response({ slots: { itemType: 'Backpack', colour: null, location: null, when: null, distinctive: null } })
    expect(readIntakeHandoff({ intake: { ownerId: 'me', response: owner } }, 'me')).not.toBeNull()
    expect(isFinder(owner)).toBe(false)
  })
})
