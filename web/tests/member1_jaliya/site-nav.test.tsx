import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { SiteNav } from '../../src/components/landing/site-nav'

describe('SiteNav', () => {
  it('renders the main navigation and opens the mobile menu', async () => {
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <SiteNav ctaHref="/login" ctaLabel="Sign in" />
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: 'FoundU' })).toHaveAttribute('href', '/')
    // The shared Button gives a link-button role="button" (Base UI), so this checks where it
    // goes rather than its role.
    expect(screen.getByText('Sign in').closest('a')).toHaveAttribute('href', '/login')
    expect(screen.getByRole('link', { name: 'How it works' })).toHaveAttribute('href', '#how-it-works')
    expect(screen.getByRole('button', { name: /open menu/i })).toBeInTheDocument()

    // jsdom has no screen width, so the desktop links are there too: opening the menu adds
    // the mobile copy beside them.
    const before = screen.getAllByRole('link', { name: 'FAQ' }).length
    await user.click(screen.getByRole('button', { name: /open menu/i }))

    const after = screen.getAllByRole('link', { name: 'FAQ' })
    expect(after).toHaveLength(before + 1)
    expect(after.at(-1)).toBeVisible()
  })
})
