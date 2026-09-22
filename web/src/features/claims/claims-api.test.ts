import { beforeEach, describe, expect, it, vi } from 'vitest'

const { post } = vi.hoisted(() => ({ post: vi.fn() }))
vi.mock('@/lib/api/client', () => ({ api: { post } }))

import { generateAiSuggestion, generateVerificationQuestions } from './claims-api'

describe('generateAiSuggestion', () => {
  beforeEach(() => post.mockReset())

  it('calls the authenticated ASP.NET endpoint with the existing camel-case contract only', () => {
    generateAiSuggestion('lost-1', 'found-1', 'Desk note')

    expect(post).toHaveBeenCalledWith('/api/match-suggestions/generate-ai', {
      lostReportId: 'lost-1',
      foundReportId: 'found-1',
      note: 'Desk note',
    })
    expect(post.mock.calls[0][0]).not.toContain('8000')
    expect(JSON.stringify(post.mock.calls[0][1])).not.toContain('serviceKey')
  })
})

describe('generateVerificationQuestions', () => {
  beforeEach(() => post.mockReset())

  it('calls only the authenticated ASP.NET claim endpoint with no private evidence payload', () => {
    generateVerificationQuestions('claim-1')

    expect(post).toHaveBeenCalledWith('/api/claims/claim-1/questions/generate')
    expect(post.mock.calls[0][0]).not.toContain('8000')
    expect(JSON.stringify(post.mock.calls[0].slice(1))).not.toContain('PrivateVerificationDetails')
    expect(JSON.stringify(post.mock.calls[0].slice(1))).not.toContain('SECRET-OWNERSHIP-DETAIL-DO-NOT-LEAK')
  })
})
