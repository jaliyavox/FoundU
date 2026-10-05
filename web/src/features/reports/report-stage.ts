import type { LostReportListItem } from './reports-api'

/**
 * Suggestion and claim progress. Finder contact and desk custody have separate notices;
 * neither one proves that a suggestion or claim exists.
 *
 * Every stage is read off real data - none of them is decorative:
 *  - Reported          no current suggestion or claim
 *  - Possible match    a suggestion exists, with no claim implied
 *  - Claim submitted   a live claim exists; review may still be pending
 *  - Back with owner   the lost report is Resolved
 */
export const LIFECYCLE = ['Reported', 'Possible Match', 'Claim Submitted', 'Back with Owner'] as const

export function stageOf(report: Pick<LostReportListItem, 'status' | 'progressStage'>) {
  if (report.status === 'Resolved') return 3
  if (report.status === 'Withdrawn') return 0
  if (['ClaimSubmitted', 'VerificationQuestions', 'RevisionRequested', 'ClaimUnderReview', 'ClaimApproved'].includes(report.progressStage ?? '')) return 2
  return report.progressStage === 'PossibleMatch' ? 1 : 0
}

export function finderContacted(report: Pick<LostReportListItem, 'foundClaimCount' | 'messageCount'>) {
  return report.foundClaimCount > 0 || report.messageCount > 0
}

export function progressDetail(report: Pick<LostReportListItem, 'status' | 'progressStage'>): string | null {
  if (report.status === 'Withdrawn') return 'Withdrawn; no longer being matched'
  if (report.status === 'Resolved') return null
  switch (report.progressStage) {
    case 'VerificationQuestions': return 'Verification questions available'
    case 'RevisionRequested': return 'More information requested for your claim'
    case 'ClaimUnderReview': return 'Claim under staff review'
    case 'ClaimApproved': return 'Claim approved; item awaiting collection'
    default: return null
  }
}

/** Largest whole unit since the report went up, as a number and its word. */
export function elapsedSince(iso: string, now = Date.now()) {
  const minutes = Math.max(0, (now - new Date(iso).getTime()) / 60000)

  if (minutes < 60) {
    const value = Math.max(1, Math.round(minutes))
    return { value, unit: value === 1 ? 'minute' : 'minutes' }
  }

  if (minutes < 60 * 24) {
    const value = Math.round(minutes / 60)
    return { value, unit: value === 1 ? 'hour' : 'hours' }
  }

  const value = Math.round(minutes / (60 * 24))
  return { value, unit: value === 1 ? 'day' : 'days' }
}
