import { describe, expect, it } from 'vitest'
import { canRoleOpen } from '../../src/routes/role-home'

describe('canRoleOpen', () => {
  it('keeps students out of the desk and admin pages', () => {
    expect(canRoleOpen('Student', '/my-reports')).toBe(true)
    expect(canRoleOpen('Student', '/items/abc')).toBe(false)
    expect(canRoleOpen('Student', '/claims')).toBe(false)
    expect(canRoleOpen('Student', '/admin/analytics')).toBe(false)
  })

  it('lets staff use the desk pages under /admin but not the admin-only ones', () => {
    expect(canRoleOpen('Staff', '/admin/support')).toBe(true)
    expect(canRoleOpen('Staff', '/admin/overview')).toBe(true)
    expect(canRoleOpen('Staff', '/admin')).toBe(false)
    expect(canRoleOpen('Staff', '/admin/moderation')).toBe(false)
    expect(canRoleOpen('Staff', '/my-reports')).toBe(false)
  })

  it('opens shared pages to everyone, including a claim by id', () => {
    for (const role of ['Student', 'Staff', 'Admin'] as const) {
      expect(canRoleOpen(role, '/claims/123')).toBe(true)
      expect(canRoleOpen(role, '/account')).toBe(true)
      expect(canRoleOpen(role, '/feed')).toBe(true)
    }
    expect(canRoleOpen('Admin', '/admin/reference')).toBe(true)
  })
})
