import { describe, expect, it } from 'vitest'
import {
  canGenerateVerificationQuestions,
  verificationQuestionGenerationMessage,
} from './verification-question-generation-feedback'

describe('verification question generation UI feedback', () => {
  it('shows only a safe success message when ASP.NET returns generated questions', () => {
    expect(verificationQuestionGenerationMessage({
      status: 'WaitingForAnswer',
      questions: [{ id: 'question-1', questionText: 'What identifying marking was on the item?' }],
    } as never)).toBe('Verification questions generated.')
  })

  it('keeps the manual workflow available when safe generation cannot produce questions', () => {
    expect(verificationQuestionGenerationMessage({ status: 'ManualReviewRequired', questions: [] } as never))
      .toBe('AI question generation is unavailable. You can still write verification questions manually.')
  })

  it('prevents duplicate AI requests while any staff claim action is pending', () => {
    expect(canGenerateVerificationQuestions(false, false, false)).toBe(true)
    expect(canGenerateVerificationQuestions(true, false, false)).toBe(false)
    expect(canGenerateVerificationQuestions(false, true, false)).toBe(false)
    expect(canGenerateVerificationQuestions(false, false, true)).toBe(false)
  })
})
