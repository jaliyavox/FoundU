import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, expect, it, vi } from 'vitest'
import { ItemDetailPage } from '../../src/features/items/item-detail-page'

const api = vi.hoisted(() => ({ item: vi.fn(), reports: vi.fn(), suggestions: vi.fn() }))
vi.mock('../../src/features/items/items-api', async (original) => ({
  ...await original<typeof import('../../src/features/items/items-api')>(), getItem: api.item, searchLostReports: api.reports,
}))
vi.mock('../../src/features/claims/claims-api', async (original) => ({
  ...await original<typeof import('../../src/features/claims/claims-api')>(), getSuggestionsForItem: api.suggestions,
}))
const item = { id: 'found', itemTypeName: 'Water Bottle', categoryName: 'Other', categoryId: 'other', itemTypeId: 'bottle',
  status: 'Unclaimed', finderName: 'Student B', generalDescription: 'Red bottle', primaryColor: 'Red', foundLocationName: 'Library',
  storageLocationName: 'Security Desk – Building A', foundAt: '2026-10-04T00:00:00Z', createdAt: '2026-10-04T00:00:00Z', privateVerificationDetails: 'NB-27 underneath cap',
}
beforeEach(() => {
  vi.clearAllMocks()
  api.item.mockResolvedValue(item)
  api.reports.mockResolvedValue({ items: [], totalCount: 0 })
  api.suggestions.mockResolvedValue([{ id: 'match', lostReportId: 'lost', lostReportDescription: 'Red bottle', status: 'Suggested', createdAt: '2026-10-04T00:00:00Z', claimId: null }])
})
function mount() {
  render(<QueryClientProvider client={new QueryClient()}><MemoryRouter initialEntries={['/items/found']}><Routes>
    <Route path="/items/:id" element={<ItemDetailPage />} />
  </Routes></MemoryRouter></QueryClientProvider>)
}
it('hides generic suggestion on a student post when no eligible report remains', async () => {
  mount()
  await screen.findByRole('heading', { name: 'Water Bottle' })
  await screen.findByText(/Waiting on them/)
  expect(screen.queryByRole('button', { name: 'Suggest to a report' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Suggest to another report' })).not.toBeInTheDocument()
})
it('offers another suggestion only when a compatible unsuggested report remains', async () => {
  api.reports.mockResolvedValue({ items: [{ id: 'another' }], totalCount: 1 })
  mount()
  expect(await screen.findByRole('button', { name: 'Suggest to another report' })).toBeEnabled()
  expect(api.reports).toHaveBeenCalledWith(1, 1, undefined, item, true)
})
it('preserves manual matching for directly staff-logged items', async () => {
  api.item.mockResolvedValue({ ...item, finderName: null })
  api.suggestions.mockResolvedValue([])
  mount()
  expect(await screen.findByRole('button', { name: 'Suggest to a report' })).toBeEnabled()
})
