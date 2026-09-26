import { describe, expect, it } from 'vitest'
import { routeAccessDecision } from './route-access'

describe('routeAccessDecision', () => {
  it('blocks anonymous users before both student and staff routes render', () => {
    expect(routeAccessDecision(null, false, ['Student'])).toBe('login')
    expect(routeAccessDecision(null, false, ['Staff', 'Admin'])).toBe('login')
  })

  it('enforces exclusive student, staff, and admin route roles', () => {
    expect(routeAccessDecision({ role: 'Student' }, false, ['Staff', 'Admin'])).toBe('forbidden')
    expect(routeAccessDecision({ role: 'Staff' }, false, ['Admin'])).toBe('forbidden')
    expect(routeAccessDecision({ role: 'Admin' }, false, ['Staff', 'Admin'])).toBe('allowed')
    expect(routeAccessDecision({ role: 'Student' }, false, ['Student'])).toBe('allowed')
  })
})
