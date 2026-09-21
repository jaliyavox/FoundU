import type { GenerateMatchSuggestionResult } from '@/features/claims/claims-api'

export function canGenerateAiSuggestion(selectedReportId: string | null, aiPending: boolean, manualPending: boolean) {
  return Boolean(selectedReportId) && !aiPending && !manualPending
}

export function aiMatchSuccessMessage(result: GenerateMatchSuggestionResult) {
  if (!result.suggestion) return 'The Matching Agent did not create a suggestion.'
  const score = result.suggestion.matchScore ?? result.score
  return `AI-assisted match suggestion created (score ${score.toFixed(2)}).`
}

export function aiMatchFailureMessage(status?: number) {
  if (status === 400 || status === 409) {
    return 'This report pair can no longer be suggested. You can still create a match manually.'
  }
  if (status === 401 || status === 403) {
    return 'You are not authorized to generate an AI-assisted suggestion.'
  }
  return 'AI-assisted matching is unavailable. You can still create a match manually.'
}

/** Closing a controlled dialog starts its next session without stale AI feedback. */
export function aiStatusAfterDialogChange(nextOpen: boolean, status: string) {
  return nextOpen ? status : ''
}
