import type { LostReportListItem } from './reports-api'

/**
 * Where a report sits in its life. Withdrawn is off this path, not a stage of it.
 *
 * Every stage is read off real data - none of them is decorative:
 *  - Reported          the report exists and is Active
 *  - Someone found it  a finder pressed the button or wrote, which is all either can mean
 *  - At the guard desk staff matched a logged found item to your report, so it is in storage
 *  - Returned          the report is Resolved
 */
export const LIFECYCLE = ['Reported', 'Someone found it', 'At the guard desk', 'Returned'] as const

export function stageOf(report: Pick<LostReportListItem, 'status' | 'foundClaimCount' | 'messageCount'>) {
  if (report.status === 'Resolved') return 3
  if (report.status === 'Matched') return 2
  return report.foundClaimCount > 0 || report.messageCount > 0 ? 1 : 0
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
