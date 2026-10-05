import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { HandoverNotice } from '../../../src/features/reports/handover-notice'
import { getHandover } from '../../../src/features/feed/handover-api'
import type { Handover } from '../../../src/features/feed/handover-api'

vi.mock('../../../src/features/feed/handover-api', () => ({
  getHandover: vi.fn(),
}))

function mount() {
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <HandoverNotice reportId="lost-1" />
  </QueryClientProvider>)
}

const handover: Handover = {
  reportId: 'lost-1', status: 'InCustody', code: '483921', startedAt: null,
  expiresAt: null, handedInAt: null, collectedAt: null,
  storageLocationName: 'Library Front Desk', finderName: 'Kasun Jay',
}

describe('owner handover notice', () => {
  beforeEach(() => vi.clearAllMocks())

  it('states actual custody and collection instructions from InCustody', async () => {
    vi.mocked(getHandover).mockResolvedValue(handover)
    mount()
    expect(await screen.findByText(/Ready to collect at Library Front Desk/)).toBeInTheDocument()
    expect(screen.getByText(/bring your student ID and quote this code/)).toBeInTheDocument()
  })

  it('does not invent custody when no handover exists', async () => {
    vi.mocked(getHandover).mockResolvedValue(null)
    mount()
    await waitFor(() => expect(getHandover).toHaveBeenCalledWith('lost-1'))
    expect(screen.queryByText(/Ready to collect/)).not.toBeInTheDocument()
  })
})
