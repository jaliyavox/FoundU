import type { LostReportListItem } from './reports-api'

/**
 * How far a lost report has got. Every stage is read off real data - none is decorative:
 *  - Reported          nothing found yet
 *  - Possible match    a suggestion exists, or a finder says they have it / is taking it in
 *  - Claim submitted   a live claim exists, or the item is at the desk waiting for its owner
 *  - Back with owner   the lost report is Resolved
 * A finder's hand-in counts as progress: the item can reach the desk with no suggestion or
 * claim ever made, and the tracker used to sit on "Reported" while it waited there.
 */
export const LIFECYCLE = ['Reported', 'Possible Match', 'Claim Submitted', 'Back with Owner'] as const

export function stageOf(report: Pick<LostReportListItem, 'status' | 'progressStage'>) {
  if (report.status === 'Resolved') return 3
  if (report.status === 'Withdrawn') return 0
  if (['ClaimSubmitted', 'VerificationQuestions', 'RevisionRequested', 'ClaimUnderReview', 'ClaimApproved', 'AtDesk'].includes(report.progressStage ?? '')) return 2
  return ['PossibleMatch', 'FinderFound', 'FinderOnTheWay'].includes(report.progressStage ?? '') ? 1 : 0
}

/** The pill's words: the finder's route has its own names for the middle stages. */
export function stageLabel(report: Pick<LostReportListItem, 'status' | 'progressStage'>) {
  if (report.status !== 'Resolved' && report.status !== 'Withdrawn') {
    switch (report.progressStage) {
      case 'FinderFound': return 'Finder Has It'
      case 'FinderOnTheWay': return 'On Its Way to the Desk'
      case 'AtDesk': return 'At the Desk'
    }
  }
  return LIFECYCLE[stageOf(report)]
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
    case 'FinderFound': return 'A finder says they have it - check your messages'
    case 'FinderOnTheWay': return 'A finder is taking it to the security desk'
    case 'AtDesk': return 'At the security desk - collect it with your collection code and student ID'
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
