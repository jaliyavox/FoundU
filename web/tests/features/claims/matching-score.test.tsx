import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { expect, it, vi } from 'vitest'
import { AgentRunsPanel } from '../../../src/features/claims/agent-runs-panel'

vi.mock('../../../src/features/claims/claims-api', () => ({
  getAgentRuns: async () => [{
    id: 'matching-run', agent: 'Matching', status: 'Completed',
    objective: 'Recommend a possible item match.',
    startedAt: '2026-10-01T10:00:00Z', completedAt: '2026-10-01T10:00:01Z',
    outcome: { recommendation: 'match_candidate', score: 0.956 },
  }],
}))

it('renders deterministic matching percentages as match scores', async () => {
  render(<QueryClientProvider client={new QueryClient()}>
    <AgentRunsPanel claimId="claim-1" />
  </QueryClientProvider>)
  expect(await screen.findByText('Match score 96%')).toBeInTheDocument()
  expect(screen.queryByText(/confidence/i)).not.toBeInTheDocument()
})
