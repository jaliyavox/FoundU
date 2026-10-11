import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'
import { writeFileSync, mkdirSync } from 'node:fs'
import { STAFF, STUDENT, call, login, signInAs } from './support/api'

/**
 * NF-A11Y: WCAG 2.1 A/AA checks with axe-core on the pages each role uses most.
 * A page passes when axe finds no "serious" or "critical" violation; minor and moderate ones
 * are recorded per page in testing/reports/accessibility/A11Y-*.json for the report.
 * Run one member's pages with: npx playwright test accessibility -g "Member 2"
 */
type Role = 'student' | 'staff' | 'admin'
type Scan = { id: string; path: string | ((token: string) => Promise<string>); as?: Role }

const ADMIN = { email: process.env.FOUNDU_ADMIN_EMAIL ?? 'admin@foundu.com', password: process.env.FOUNDU_ADMIN_PASSWORD ?? '' }

/** A claim staff can open, so the claim page is scanned with real content on it. */
async function firstClaim(token: string) {
  const queue = await call('GET', '/api/claims?pageSize=1', token)
  return `/claims/${queue.items[0].id}`
}

/** A found item in storage, for the staff item page. */
async function firstItem(token: string) {
  const items = await call('GET', '/api/found-reports?pageSize=1', token)
  return `/items/${items.items[0].id}`
}

// Each member's pages, in their business component; the public landing page and the AI
// assistant page stay with the group.
const groups: { title: string; pages: Scan[] }[] = [
  {
    title: 'Member 1 - Jaliya: accounts, administration, support',
    pages: [
      { id: 'A11Y-02', path: '/login' },
      { id: 'A11Y-03', path: '/register' },
      { id: 'A11Y-12', path: '/admin/users', as: 'admin' },
      { id: 'A11Y-13', path: '/admin/support', as: 'staff' },
      { id: 'A11Y-08', path: '/support', as: 'student' },
    ],
  },
  {
    title: 'Member 2 - Ranasinghe: lost item reporting and tracking',
    pages: [
      { id: 'A11Y-14', path: '/my-reports/new', as: 'student' },
      { id: 'A11Y-05', path: '/my-reports', as: 'student' },
      { id: 'A11Y-04', path: '/feed' },
    ],
  },
  {
    title: 'Member 3 - Uthpala: found items and matching',
    pages: [
      { id: 'A11Y-11', path: '/items/new', as: 'staff' },
      { id: 'A11Y-09', path: '/items', as: 'staff' },
      { id: 'A11Y-15', path: '/found', as: 'student' },
      { id: 'A11Y-17', path: firstItem, as: 'staff' },
    ],
  },
  {
    title: 'Member 4 - Braveena: claims and verification',
    pages: [
      { id: 'A11Y-06', path: '/my-claims', as: 'student' },
      { id: 'A11Y-10', path: '/claims', as: 'staff' },
      { id: 'A11Y-16', path: firstClaim, as: 'staff' },
    ],
  },
  {
    title: 'Group',
    pages: [
      { id: 'A11Y-01', path: '/' },
      { id: 'A11Y-07', path: '/ask-foundu', as: 'student' },
    ],
  },
]

for (const group of groups) {
  test.describe(`Accessibility (axe-core, WCAG 2.1 AA) - ${group.title}`, () => {
    for (const { id, path, as } of group.pages) {
      const label = typeof path === 'string' ? path : path === firstClaim ? '/claims/:id' : '/items/:id'
      test(`${id} ${label}${as ? ` as ${as}` : ''} has no serious or critical violations`, async ({ page }) => {
        let target = typeof path === 'string' ? path : ''
        if (as) {
          if (as === 'admin') test.skip(!ADMIN.password, 'set FOUNDU_ADMIN_PASSWORD to scan the admin pages')
          const who = as === 'admin' ? ADMIN : as === 'staff' ? STAFF : STUDENT
          const session = await login(who.email, who.password)
          if (typeof path !== 'string') target = await path(session.accessToken)
          await signInAs(page, session, target)
        } else {
          await page.goto(target)
        }
        await page.waitForLoadState('networkidle')
        const scan = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze()
        const found = scan.violations.map((v) => ({ rule: v.id, impact: v.impact, help: v.help, nodes: v.nodes.length, targets: v.nodes.map((n) => n.target.join(' ')), html: v.nodes.slice(0, 3).map((n) => n.html.slice(0, 200)) }))
        mkdirSync('../reports/accessibility', { recursive: true })
        writeFileSync(`../reports/accessibility/${id}.json`, JSON.stringify({ id, path: target, as: as ?? 'anonymous', passes: scan.passes.length, violations: found }, null, 2))
        const blocking = found.filter((v) => v.impact === 'serious' || v.impact === 'critical')
        expect(blocking, JSON.stringify(blocking, null, 2)).toEqual([])
      })
    }
  })
}
