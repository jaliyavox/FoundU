import { describe, expect, it } from 'vitest'
import { ApiError, assetUrl } from './client'

describe('assetUrl', () => {
  it('resolves a host-relative upload against the API origin, not the page origin', () => {
    // In dev the page is on 5173 and the API elsewhere; a bare "/uploads/..." would hit
    // Vite's SPA fallback and come back as HTML.
    expect(assetUrl('/uploads/lost-reports/a/b.jpg')).toBe('http://api.test/uploads/lost-reports/a/b.jpg')
  })

  it('leaves an absolute URL alone', () => {
    expect(assetUrl('https://cdn.example/x.png')).toBe('https://cdn.example/x.png')
  })
})

describe('ApiError', () => {
  it('prefers the human-readable detail as its message', () => {
    const error = new ApiError(409, { title: 'Conflict', status: 409, detail: 'This claim has already been decided.' })
    expect(error.message).toBe('This claim has already been decided.')
    expect(error.status).toBe(409)
  })

  it('falls back to the title, then to the status', () => {
    expect(new ApiError(500, { title: 'Server error', status: 500 }).message).toBe('Server error')
    expect(new ApiError(502, { title: '', status: 502 }).message).toBe('Request failed with status 502')
  })

  it('exposes field errors keyed by the C# property name, and an empty map otherwise', () => {
    const invalid = new ApiError(400, {
      title: 'Validation failed',
      status: 400,
      errors: { Description: ['Describe the item in at least 10 characters so it can be matched.'] },
    })
    expect(invalid.fieldErrors.Description).toHaveLength(1)
    expect(new ApiError(404, { title: 'Not found', status: 404 }).fieldErrors).toEqual({})
  })
})
