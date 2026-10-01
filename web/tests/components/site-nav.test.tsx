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
    expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute('href', '/login')
    expect(screen.getByRole('link', { name: 'How it works' })).toHaveAttribute('href', '#how-it-works')
    expect(screen.getByRole('button', { name: /open menu/i })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /open menu/i }))

    expect(screen.getByRole('link', { name: 'FAQ' })).toBeVisible()
  })
})
