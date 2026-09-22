import type { ClaimDetail } from './claims-api'

const unavailableMessage = 'AI question generation is unavailable. You can still write verification questions manually.'

/** Keep UI feedback bounded to the safe claim detail returned by ASP.NET. */
export function verificationQuestionGenerationMessage(claim: Pick<ClaimDetail, 'status' | 'questions'>) {
  return claim.status === 'WaitingForAnswer' && claim.questions.length > 0
    ? 'Verification questions generated.'
    : unavailableMessage
}

export function canGenerateVerificationQuestions(aiPending: boolean, manualPending: boolean, decisionPending: boolean) {
  return !aiPending && !manualPending && !decisionPending
}
