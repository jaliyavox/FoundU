import { describe, expect, it } from 'vitest'
import { elapsedSince, finderContacted, LIFECYCLE, progressDetail, stageOf } from '../../../src/features/reports/report-stage'

describe('stageOf', () => {
  const states = [
    ['newly reported', 'Active', 'Reported', 0, 0, 0, null],
    ['finder contacted owner', 'Active', 'Reported', 1, 0, 0, null],
    ['staff received item without a claim', 'Matched', 'Reported', 1, 0, 0, null],
    ['suggestion created', 'Active', 'PossibleMatch', 0, 0, 1, null],
    ['claim submitted', 'Matched', 'ClaimSubmitted', 0, 0, 2, null],
    ['verification questions', 'Matched', 'VerificationQuestions', 0, 0, 2, 'Verification questions available'],
    ['follow-up requested', 'Matched', 'RevisionRequested', 0, 0, 2, 'More information requested for your claim'],
    ['claim under review', 'Matched', 'ClaimUnderReview', 0, 0, 2, 'Claim under staff review'],
    ['claim approved but still at desk', 'Matched', 'ClaimApproved', 0, 0, 2, 'Claim approved; item awaiting collection'],
    ['approved and returned', 'Resolved', 'Resolved', 0, 0, 3, null],
    ['rejected claim', 'Active', 'Reported', 0, 0, 0, null],
    ['withdrawn report', 'Withdrawn', 'Reported', 0, 0, 0, 'Withdrawn; no longer being matched'],
  ] as const

  it.each(states)('%s', (_name, status, progressStage, foundClaimCount, messageCount, stage, detail) => {
    expect(stageOf({ status, progressStage })).toBe(stage)
    expect(finderContacted({ foundClaimCount, messageCount })).toBe(foundClaimCount > 0 || messageCount > 0)
    expect(progressDetail({ status, progressStage })).toBe(detail)
  })

  it('a finder message alone signals contact without implying a suggestion', () => {
    expect(finderContacted({ foundClaimCount: 0, messageCount: 1 })).toBe(true)
    expect(stageOf({ status: 'Active', progressStage: 'Reported' })).toBe(0)
  })

  it('does not infer a claim or custody from legacy Matched without progressStage', () => {
    expect(stageOf({ status: 'Matched' })).toBe(0)
  })

  it('has one label per stage', () => {
    expect(LIFECYCLE).toHaveLength(4)
    expect(LIFECYCLE).toEqual(['Reported', 'Possible Match', 'Claim Submitted', 'Back with Owner'])
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
