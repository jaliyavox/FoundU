import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, expect, it, vi } from 'vitest'
import { ClaimQueuePage } from '../../src/features/claims/claim-queue-page'

const api = vi.hoisted(() => ({ queue: vi.fn() }))
vi.mock('../../src/features/claims/claims-api', async (importOriginal) => ({
  ...await importOriginal<typeof import('../../src/features/claims/claims-api')>(),
  getClaimQueue: api.queue,
}))
afterEach(() => vi.useRealTimers())
it('polls a submitted mobile claim into the queue without a restart', async () => {
  vi.useFakeTimers()
  let submitted = false
  api.queue.mockImplementation(async () => ({ page: 1, totalPages: 1, totalCount: submitted ? 1 : 0,
    items: submitted ? [{ id: 'claim', status: 'Pending', itemTypeName: 'Water Bottle', categoryName: 'Other', studentName: 'Student A',
      lostReportId: 'lost-bottle', foundReportId: 'found-bottle', storageLocationName: 'Security Desk – Building A', matchScore: .85,
      verificationStatus: 'Pending', unansweredQuestionCount: 0, createdAt: '2026-10-04T00:00:00Z', updatedAt: '2026-10-04T00:00:00Z' }] : [],
  }))
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(<QueryClientProvider client={client}><MemoryRouter><ClaimQueuePage /></MemoryRouter></QueryClientProvider>)
  await act(async () => { await vi.advanceTimersByTimeAsync(50) })
  expect(screen.queryByText('Water Bottle')).not.toBeInTheDocument()
  submitted = true
  await act(async () => { await vi.advanceTimersByTimeAsync(5100) })
  expect(screen.getByText('Water Bottle')).toBeInTheDocument()
  expect(screen.getByText(/Student A/)).toBeInTheDocument()
  expect(screen.getByText(/Security Desk – Building A/)).toBeInTheDocument()
  expect(screen.getByText(/Match score 85%/)).toBeInTheDocument()
  expect(screen.getByText(/lost-bottle/)).toBeInTheDocument()
  client.clear()
})
