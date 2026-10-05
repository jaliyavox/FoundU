import { describe, expect, it } from 'vitest'
import { elapsedSince, LIFECYCLE, stageOf } from './report-stage'

describe('stageOf', () => {
  it('starts at Reported for a fresh active report', () => {
    expect(stageOf({ status: 'Active', foundClaimCount: 0, messageCount: 0 })).toBe(0)
  })

  it('advances when someone presses "I found this", even with no message', () => {
    expect(stageOf({ status: 'Active', foundClaimCount: 1, messageCount: 0 })).toBe(1)
  })

  it('advances on a message alone - a message on a lost report only ever means "found it"', () => {
    expect(stageOf({ status: 'Active', foundClaimCount: 0, messageCount: 1 })).toBe(1)
  })

  it('reads Matched as "at the guard desk" regardless of finder signals', () => {
    expect(stageOf({ status: 'Matched', foundClaimCount: 0, messageCount: 0 })).toBe(2)
  })

  it('reads Resolved as Returned', () => {
    expect(stageOf({ status: 'Resolved', foundClaimCount: 5, messageCount: 5 })).toBe(3)
  })

  it('treats a stale payload without counts as not found yet, rather than crashing', () => {
    // An API build that predates foundClaimCount sends undefined; undefined > 0 is false.
    expect(stageOf({ status: 'Active' } as never)).toBe(0)
  })

  it('has one label per stage', () => {
    expect(LIFECYCLE).toHaveLength(4)
    expect(LIFECYCLE[3]).toBe('Returned')
  })
})

describe('elapsedSince', () => {
  const now = Date.parse('2026-09-15T12:00:00Z')

  it('never says zero minutes', () => {
    expect(elapsedSince('2026-09-15T11:59:50Z', now)).toEqual({ value: 1, unit: 'minute' })
  })

  it('uses the largest whole unit', () => {
    expect(elapsedSince('2026-09-15T11:15:00Z', now)).toEqual({ value: 45, unit: 'minutes' })
    expect(elapsedSince('2026-09-15T09:00:00Z', now)).toEqual({ value: 3, unit: 'hours' })
    expect(elapsedSince('2026-09-13T12:00:00Z', now)).toEqual({ value: 2, unit: 'days' })
  })

  it('singularises one hour and one day', () => {
    expect(elapsedSince('2026-09-15T11:00:00Z', now).unit).toBe('hour')
    expect(elapsedSince('2026-09-14T12:00:00Z', now).unit).toBe('day')
  })

  it('does not go negative for a timestamp slightly in the future', () => {
    expect(elapsedSince('2026-09-15T12:00:30Z', now)).toEqual({ value: 1, unit: 'minute' })
  })
})
