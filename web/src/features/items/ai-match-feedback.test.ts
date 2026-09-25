import { describe, expect, it } from 'vitest'
import {
  aiMatchFailureMessage,
  aiMatchSuccessMessage,
  aiStatusAfterDialogChange,
  canGenerateAiSuggestion,
} from './ai-match-feedback'

describe('AI matching UI feedback', () => {
  it('enables only one selected, non-pending AI request', () => {
    expect(canGenerateAiSuggestion(null, false, false)).toBe(false)
    expect(canGenerateAiSuggestion('lost-1', true, false)).toBe(false)
    expect(canGenerateAiSuggestion('lost-1', false, true)).toBe(false)
    expect(canGenerateAiSuggestion('lost-1', false, false)).toBe(true)
  })

  it('keeps no-match and manual-review results neutral', () => {
    expect(aiMatchSuccessMessage({ recommendation: 'no_match', score: 0, suggestion: null }))
      .toBe('The Matching Agent did not create a suggestion.')
    expect(aiMatchSuccessMessage({ recommendation: 'manual_review', score: 0, suggestion: null }))
      .toBe('The Matching Agent did not create a suggestion.')
  })

  it('formats safe score feedback for the close-on-success toast', () => {
    expect(aiMatchSuccessMessage({
      recommendation: 'match_candidate',
      score: 0.85,
      suggestion: { matchScore: 0.85 },
    } as never)).toBe('AI-assisted match suggestion created (score 0.85).')
  })

  it('clears transient AI feedback when the dialog closes before it is reopened', () => {
    expect(aiStatusAfterDialogChange(false, 'The Matching Agent did not create a suggestion.')).toBe('')
    expect(aiStatusAfterDialogChange(true, '')).toBe('')
  })

  it('uses safe failure copy that preserves the manual workflow', () => {
    expect(aiMatchFailureMessage(409)).toContain('create a match manually')
    expect(aiMatchFailureMessage(503)).toBe('AI-assisted matching is unavailable. You can still create a match manually.')
  })
})
