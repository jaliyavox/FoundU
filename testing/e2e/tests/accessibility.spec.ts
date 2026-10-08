import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'
import { writeFileSync, mkdirSync } from 'node:fs'
import { STAFF, STUDENT, login, signInAs } from './support/api'

/**
 * NF-A11Y: WCAG 2.1 A/AA checks with axe-core on the pages each role uses most.
 * A page passes when axe finds no "serious" or "critical" violation; minor and moderate ones
 * are recorded per page in testing/reports/accessibility/A11Y-*.json for the report.
 */
const pages: { id: string; path: string; as?: 'student' | 'staff' }[] = [
  { id: 'A11Y-01', path: '/' },
  { id: 'A11Y-02', path: '/login' },
  { id: 'A11Y-03', path: '/register' },
  { id: 'A11Y-04', path: '/feed' },
  { id: 'A11Y-05', path: '/my-reports', as: 'student' },
  { id: 'A11Y-06', path: '/my-claims', as: 'student' },
  { id: 'A11Y-07', path: '/ask-foundu', as: 'student' },
  { id: 'A11Y-08', path: '/support', as: 'student' },
  { id: 'A11Y-09', path: '/items', as: 'staff' },
  { id: 'A11Y-10', path: '/claims', as: 'staff' },
  { id: 'A11Y-11', path: '/items/new', as: 'staff' },
]

test.describe('Accessibility (axe-core, WCAG 2.1 AA)', () => {
  for (const { id, path, as } of pages) {
    test(`${id} ${path}${as ? ` as ${as}` : ''} has no serious or critical violations`, async ({ page }) => {
      if (as) {
        const who = as === 'staff' ? STAFF : STUDENT
        await signInAs(page, await login(who.email, who.password), path)
      } else {
        await page.goto(path)
      }
      await page.waitForLoadState('networkidle')
      const scan = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze()
      const found = scan.violations.map((v) => ({ rule: v.id, impact: v.impact, help: v.help, nodes: v.nodes.length, targets: v.nodes.map((n) => n.target.join(' ')), html: v.nodes.slice(0, 3).map((n) => n.html.slice(0, 200)) }))
      mkdirSync('../reports/accessibility', { recursive: true })
      writeFileSync(`../reports/accessibility/${id}.json`, JSON.stringify({ id, path, as: as ?? 'anonymous', passes: scan.passes.length, violations: found }, null, 2))
      const blocking = found.filter((v) => v.impact === 'serious' || v.impact === 'critical')
      expect(blocking, JSON.stringify(blocking, null, 2)).toEqual([])
    })
  }
})
