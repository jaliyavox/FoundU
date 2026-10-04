import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { LinkReportDialog } from '../../../src/features/items/link-report-dialog'
import type { FoundReportDetail } from '../../../src/features/items/items-api'

const api = vi.hoisted(() => ({ search: vi.fn(), existing: vi.fn(), manual: vi.fn(), ai: vi.fn() }))
vi.mock('../../../src/features/items/items-api', () => ({ searchLostReports: api.search }))
vi.mock('../../../src/features/claims/claims-api', () => ({
  getSuggestionsForItem: api.existing, createSuggestion: api.manual, generateAiSuggestion: api.ai,
}))

const item = { id: 'found', itemTypeName: 'Backpack', status: 'Unclaimed' } as FoundReportDetail
const report = (id: string) => ({ id, itemTypeName: `Backpack ${id}`, primaryColor: 'Blue',
  description: 'Blue backpack', lastSeenLocationName: 'Library', estimatedLostFromAt: '2026-10-01T12:00:00Z' })

function mount() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const closed = vi.fn()
  render(<QueryClientProvider client={client}>
    <LinkReportDialog item={item} open onOpenChange={closed} />
  </QueryClientProvider>)
  return { client, closed }
}

describe('staff suggestion picker', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    api.search.mockResolvedValue({ items: [report('existing'), report('new')] })
    api.existing.mockResolvedValue([{ id: 'suggestion', lostReportId: 'existing' }])
    api.manual.mockResolvedValue({ id: 'manual' })
    api.ai.mockResolvedValue({ recommendation: 'match_candidate', score: 0.96, suggestion: { id: 'ai', matchScore: 0.96 } })
  })

  it('disables existing pairs and still sends a different eligible report manually', async () => {
    const user = userEvent.setup()
    const { closed } = mount()
    const existing = await screen.findByRole('button', { name: /Backpack existing/ })
    expect(existing).toBeDisabled()
    expect(screen.getByText('Already suggested')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Suggest it' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: /Backpack new/ }))
    await user.click(screen.getByRole('button', { name: 'Suggest it' }))
    await waitFor(() => expect(api.manual).toHaveBeenCalledWith('new', 'found', undefined))
    expect(closed).toHaveBeenCalledWith(false)
  })

  it('offers AI matching for a directly logged item through the backend client', async () => {
    const user = userEvent.setup()
    api.existing.mockResolvedValue([])
    mount()
    await user.click(await screen.findByRole('button', { name: /Backpack new/ }))
    await user.click(screen.getByRole('button', { name: 'Generate AI Match Suggestion' }))
    await waitFor(() => expect(api.ai).toHaveBeenCalledWith('new', 'found', undefined))
    expect(api.manual).not.toHaveBeenCalled()
  })

  it('explains why all displayed candidates are disabled', async () => {
    api.search.mockResolvedValue({ items: [report('existing')] })
    mount()
    expect(await screen.findByText(/All reports in this search are already suggested/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Suggest it' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Generate AI Match Suggestion' })).toBeDisabled()
  })
})
