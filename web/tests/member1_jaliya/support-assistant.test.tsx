import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AssistantResponse } from '../../src/features/support/support-api'

const askSupportAssistant = vi.fn<(message: string, history: unknown[], lastTopic: string | null) => Promise<AssistantResponse>>()
const createTicket = vi.fn()

vi.mock('../../src/features/support/support-api', async importOriginal => ({
  ...(await importOriginal<typeof import('../../src/features/support/support-api')>()),
  askSupportAssistant: (message: string, history: unknown[], lastTopic: string | null) => askSupportAssistant(message, history, lastTopic),
  createTicket: (input: unknown) => createTicket(input),
}))

const { SupportAssistant } = await import('../../src/features/support/support-assistant')

function mount(onTicketOpened = vi.fn()) {
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { mutations: { retry: false } } })}>
      <SupportAssistant onTicketOpened={onTicketOpened} />
    </QueryClientProvider>,
  )
  return onTicketOpened
}

describe('SupportAssistant', () => {
  beforeEach(() => {
    askSupportAssistant.mockReset()
    createTicket.mockReset()
  })

  it('answers, and "No, I still need help" goes back with the topic it was about', async () => {
    const user = userEvent.setup()
    askSupportAssistant.mockResolvedValueOnce({
      phase: 'answered',
      reply: 'Your collection code is on the claim page. Did that solve it?',
      topic: 'collection_code',
      ticket: null,
    })
    askSupportAssistant.mockResolvedValueOnce({
      phase: 'escalate',
      reply: 'Of course - this needs a person.',
      topic: 'collection_code',
      ticket: { subject: 'where is my code', category: 'Collection', body: 'Raised through the FoundU assistant.' },
    })
    mount()

    await user.type(screen.getByPlaceholderText(/collection code is not accepted/i), 'where is my code')
    await user.click(screen.getByRole('button', { name: 'Ask' }))
    expect(await screen.findByText(/on the claim page/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'No, I still need help' }))

    await waitFor(() => expect(askSupportAssistant).toHaveBeenCalledTimes(2))
    const [, history, lastTopic] = askSupportAssistant.mock.calls[1]
    expect(lastTopic).toBe('collection_code')
    // The page's own greeting is not sent as if the agent had said it.
    expect(history).toEqual([
      { role: 'user', text: 'where is my code' },
      { role: 'assistant', text: 'Your collection code is on the claim page. Did that solve it?' },
    ])
    expect(await screen.findByDisplayValue('where is my code')).toBeInTheDocument()
  })

  it('sends the drafted ticket only when the person presses send, marked as via the assistant', async () => {
    const user = userEvent.setup()
    askSupportAssistant.mockResolvedValueOnce({
      phase: 'escalate',
      reply: 'A forgotten password has to be reset by the desk.',
      topic: 'cannot_sign_in',
      ticket: { subject: 'Locked out', category: 'Account', body: 'Raised through the FoundU assistant.' },
    })
    createTicket.mockResolvedValueOnce({ id: 'ticket-1' })
    const onTicketOpened = mount()

    await user.type(screen.getByPlaceholderText(/collection code is not accepted/i), 'I forgot my password')
    await user.click(screen.getByRole('button', { name: 'Ask' }))

    const subject = await screen.findByLabelText('Subject')
    expect(createTicket).not.toHaveBeenCalled()
    await user.clear(subject)
    await user.type(subject, 'Locked out of my account')
    await user.click(screen.getByRole('button', { name: 'Send to the support team' }))

    await waitFor(() => expect(onTicketOpened).toHaveBeenCalledWith('ticket-1'))
    expect(createTicket).toHaveBeenCalledWith({
      subject: 'Locked out of my account',
      category: 'Account',
      body: 'Raised through the FoundU assistant.',
      viaAssistant: true,
    })
  })
})
