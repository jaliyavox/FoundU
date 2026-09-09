import { api } from '@/lib/api/client'
import type { PagedResult } from '@/lib/api/types'

/* ------------------------------------------------------------------ shapes */

/** Mirrors FoundU.Application.FoundReports.Dtos.FoundReportSummaryDto - the student-safe
 *  shape. It deliberately carries no hidden verification evidence. */
export interface FoundItemSummary {
  id: string
  categoryName: string
  itemTypeName: string
  foundLocationName: string
  generalDescription: string
  primaryColor: string | null
  foundAt: string
  status: string
}

export interface MatchSuggestion {
  id: string
  lostReportId: string
  lostReportDescription: string
  foundItem: FoundItemSummary
  status: 'Suggested' | 'Confirmed' | 'Dismissed'
  note: string | null
  isAgentGenerated: boolean
  matchScore: number | null
  /** Set once a claim has been opened from this suggestion. */
  claimId: string | null
  createdAt: string
}

export interface ClaimQuestion {
  id: string
  questionText: string
  answerText: string | null
  answeredAt: string | null
}

export interface ClaimListItem {
  id: string
  status: ClaimStatus
  categoryName: string
  itemTypeName: string
  studentName: string
  unansweredQuestionCount: number
  createdAt: string
  updatedAt: string
}

export interface ClaimDetail {
  id: string
  status: ClaimStatus
  studentId: string
  studentName: string
  lostReportId: string
  lostReportDescription: string
  foundItem: FoundItemSummary
  questions: ClaimQuestion[]
  decision: 'Approved' | 'Rejected' | 'RevisionRequested' | null
  decisionReason: string | null
  decidedByName: string | null
  decidedAt: string | null
  createdAt: string
  updatedAt: string
}

export type ClaimStatus =
  | 'Pending'
  | 'WaitingForAnswer'
  | 'UnderReview'
  | 'RevisionRequested'
  | 'Approved'
  | 'Rejected'
  | 'Cancelled'
  | 'ManualReviewRequired'

/* --------------------------------------------------------------- suggestions */

export const getMySuggestions = (page = 1, pageSize = 20) =>
  api.get<PagedResult<MatchSuggestion>>(`/api/match-suggestions/mine?page=${page}&pageSize=${pageSize}`)

export const getSuggestionsForItem = (foundReportId: string) =>
  api.get<MatchSuggestion[]>(`/api/match-suggestions/for-item/${foundReportId}`)

export const createSuggestion = (lostReportId: string, foundReportId: string, note?: string) =>
  api.post<MatchSuggestion>('/api/match-suggestions', { lostReportId, foundReportId, note })

export const dismissSuggestion = (id: string, reason?: string) =>
  api.post<MatchSuggestion>(`/api/match-suggestions/${id}/dismiss`, { reason })

/* -------------------------------------------------------------------- claims */

export const createClaim = (lostReportId: string, foundReportId: string) =>
  api.post<ClaimDetail>('/api/claims', { lostReportId, foundReportId })

export const getMyClaims = (page = 1, pageSize = 20) =>
  api.get<PagedResult<ClaimListItem>>(`/api/claims/mine?page=${page}&pageSize=${pageSize}`)

export const getClaimQueue = (page: number, pageSize: number, status?: string) => {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (status) params.set('status', status)
  return api.get<PagedResult<ClaimListItem>>(`/api/claims?${params}`)
}

export const getClaim = (id: string) => api.get<ClaimDetail>(`/api/claims/${id}`)

export const addQuestions = (id: string, questions: string[]) =>
  api.post<ClaimDetail>(`/api/claims/${id}/questions`, { questions })

export const submitAnswers = (id: string, answers: { questionId: string; answerText: string }[]) =>
  api.post<ClaimDetail>(`/api/claims/${id}/answers`, { answers })

export const decideClaim = (id: string, decision: string, reason?: string) =>
  api.post<ClaimDetail>(`/api/claims/${id}/decision`, { decision, reason })

export const cancelClaim = (id: string, reason?: string) =>
  api.post<ClaimDetail>(`/api/claims/${id}/cancel`, { reason })

/* ------------------------------------------------------------------ display */

/**
 * What each status means to the person reading it, in their own terms - a student is told
 * what to do next, staff are told what the queue is waiting on.
 */
export const CLAIM_STATUS_COPY: Record<ClaimStatus, { label: string; student: string; tone: Tone }> = {
  Pending: {
    label: 'Submitted',
    student: 'The desk has your claim and will be in touch with a few questions.',
    tone: 'waiting',
  },
  WaitingForAnswer: {
    label: 'Questions to answer',
    student: 'Answer the questions below to show the item is yours.',
    tone: 'action',
  },
  UnderReview: {
    label: 'Under review',
    student: 'Your answers are with the desk.',
    tone: 'waiting',
  },
  RevisionRequested: {
    label: 'More detail needed',
    student: 'The desk needs a fuller answer before they can decide.',
    tone: 'action',
  },
  Approved: {
    label: 'Approved',
    student: 'It is yours - collect it from the desk holding it.',
    tone: 'good',
  },
  Rejected: {
    label: 'Not approved',
    student: 'The desk could not match this item to you.',
    tone: 'bad',
  },
  Cancelled: {
    label: 'Cancelled',
    student: 'You withdrew this claim.',
    tone: 'muted',
  },
  ManualReviewRequired: {
    label: 'With a supervisor',
    student: 'A supervisor is looking at this one.',
    tone: 'waiting',
  },
}

export type Tone = 'good' | 'bad' | 'action' | 'waiting' | 'muted'

export const TONE_STYLES: Record<Tone, string> = {
  good: 'border-brand-green/35 bg-brand-green/12 text-brand-forest dark:text-brand-sage',
  bad: 'border-destructive/35 bg-destructive/10 text-destructive',
  action: 'border-amber-500/40 bg-amber-500/12 text-amber-700 dark:text-amber-300',
  waiting: 'border-foreground/12 bg-foreground/5 text-muted-foreground',
  muted: 'border-foreground/10 bg-transparent text-muted-foreground',
}
