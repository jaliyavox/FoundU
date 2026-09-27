import { api } from '@/lib/api/client'

export interface IntakeSlots {
  itemType: string | null
  colour: string | null
  location: string | null
  when: string | null
  distinctive: string | null
  /** Which side: an owner looking ('lost', the default when unset) or a finder ('found'). */
  intent?: IntakeIntent | null
}

export type IntakeIntent = 'lost' | 'found'

export const isFinder = (response: Pick<IntakeResponse, 'slots'> | null) => response?.slots.intent === 'found'

/** Slots with only the side known - what the page sends when someone picks "I found something". */
export const slotsFor = (intent: IntakeIntent): IntakeSlots =>
  ({ itemType: null, colour: null, location: null, when: null, distinctive: null, intent })

export interface IntakeResponse {
  phase: 'collecting' | 'matched' | 'no_match' | 'unavailable'
  reply: string
  slots: IntakeSlots
  draft: {
    categoryId: string | null
    itemTypeId: string | null
    locationId: string | null
    description: string
    primaryColor: string | null
    when: string | null
  }
  match: {
    id: string
    /** 'desk'/'post': a found item, shown to an owner. 'lost': an open lost report, shown to a finder. */
    kind: 'desk' | 'post' | 'lost'
    itemType: string
    colour: string | null
    location: string
    description: string
  } | null
}

export const askIntake = (message: string, slots?: IntakeSlots) =>
  api.post<IntakeResponse>('/api/intake', { message, slots })

export interface IntakeHandoff {
  ownerId: string
  response: IntakeResponse
}

// Router state is only a draft convenience. Never restore another account's conversation.
export function readIntakeHandoff(state: unknown, ownerId: string | undefined): IntakeHandoff | null {
  if (!ownerId || !isObject(state) || !isObject(state.intake)) return null
  const handoff = state.intake
  if (handoff.ownerId !== ownerId || !isObject(handoff.response)) return null
  const response = handoff.response
  if (!['collecting', 'matched', 'no_match', 'unavailable'].includes(String(response.phase))
    || typeof response.reply !== 'string' || !isObject(response.draft) || !isObject(response.slots)) return null
  const draft = response.draft
  const slots = response.slots
  if (typeof draft.description !== 'string'
    || ![draft.categoryId, draft.itemTypeId, draft.locationId, draft.primaryColor, draft.when].every(optionalText)
    || !['itemType', 'colour', 'location', 'when', 'distinctive'].every(key => optionalText(slots[key]))
    || ![undefined, null, 'lost', 'found'].includes(slots.intent as string | null | undefined)) return null
  const match = response.match
  if (match !== null && (!isObject(match) || !['desk', 'post', 'lost'].includes(String(match.kind))
    || !['id', 'itemType', 'location', 'description'].every(key => typeof match[key] === 'string')
    || !optionalText(match.colour))) return null
  return handoff as unknown as IntakeHandoff
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
function optionalText(value: unknown) { return value === null || typeof value === 'string' }
