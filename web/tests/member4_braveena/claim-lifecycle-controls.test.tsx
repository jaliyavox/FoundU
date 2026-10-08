import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { StaffControls } from '../../src/features/claims/claim-detail-page'
import type { ClaimDetail } from '../../src/features/claims/claims-api'

const api = vi.hoisted(() => ({ followUp: vi.fn() }))
vi.mock('../../src/features/claims/claims-api', async (importOriginal) => ({
  ...await importOriginal<typeof import('../../src/features/claims/claims-api')>(),
  requestFollowUp: api.followUp,
}))
function mount(status: string, answered = false, questions = true) {
  const claim = { id: 'claim', status, foundItem: { status: 'Unclaimed' },
    questions: questions ? [{ id: 'q', questionText: 'What identifying mark is underneath the bottle cap?', answerText: answered ? 'NB-27 underneath the cap.' : null }] : [],
    verificationForStaff: answered ? { score: 74, rationale: 'More evidence needs review.', recommendation: 'More information required — manual review.' } : null,
  } as ClaimDetail
  render(<QueryClientProvider client={new QueryClient()}><StaffControls claim={claim} /></QueryClientProvider>)
}
describe('staff claim lifecycle controls', () => {
  beforeEach(() => { vi.clearAllMocks(); api.followUp.mockResolvedValue({ status: 'RevisionRequested' }) })
  it('offers initial generation only before questions exist, and gates approval', () => {
    mount('Pending', false, false)
    expect(screen.getByRole('button', { name: 'Generate AI Verification Questions' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Ask for more detail' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Reject' })).toBeDisabled()
  })
  it('shows waiting state and never generates a second set of initial questions', () => {
    mount('WaitingForAnswer')
    expect(screen.getByText('Waiting for claimant’s answer.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Generate AI Verification Questions' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Send to the claimant' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Ask for more detail' })).toBeDisabled()
  })
  it('shows advisory percentage and sends a confirmed follow-up with new physical evidence', async () => {
    const user = userEvent.setup()
    mount('ManualReviewRequired', true)
    expect(screen.getByText('Verification score: 74%')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled()
    expect(screen.queryByRole('button', { name: 'Generate AI Verification Questions' })).not.toBeInTheDocument()
    await user.type(screen.getByLabelText('Additional observable hidden detail (staff only)'), 'A small scratch inside the base')
    await user.type(screen.getByLabelText('Confirm a distinct, non-leading follow-up question'), 'Describe any damage inside the base.')
    await user.click(screen.getByRole('button', { name: 'Ask for more detail' }))
    await waitFor(() => expect(api.followUp).toHaveBeenCalledWith('claim', 'Describe any damage inside the base.', 'A small scratch inside the base'))
  })
  it('locks approval while awaiting an additional answer', () => {
    mount('RevisionRequested', true)
    expect(screen.getByText('Waiting for additional answer.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Ask for more detail' })).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Generate AI Verification Questions' })).not.toBeInTheDocument()
  })
})
