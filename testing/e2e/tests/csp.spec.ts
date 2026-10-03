import { expect, test, type Page } from '@playwright/test'
import { STAFF, STUDENT, login } from './support/api'

/**
 * NF-SEC-CSP: the production build, served with the headers render.yaml ships
 * (testing/security/csp/serve-with-render-headers.mjs), must work with no CSP violation.
 * Runs only when CSP_URL is set, e.g. CSP_URL=http://127.0.0.1:4173.
 */
const base = process.env.CSP_URL
test.skip(!base, 'set CSP_URL to the header-faithful server')

async function watch(page: Page) {
  const violations: string[] = []
  await page.addInitScript(() => {
    document.addEventListener('securitypolicyviolation', (e) => {
      ;(window as unknown as { __csp: string[] }).__csp ??= []
      ;(window as unknown as { __csp: string[] }).__csp.push(`${e.violatedDirective} ${e.blockedURI}`)
    })
  })
  page.on('console', (m) => { if (/Content Security Policy/i.test(m.text())) violations.push(m.text()) })
  return async () => violations.concat(await page.evaluate(() => (window as unknown as { __csp?: string[] }).__csp ?? []))
}

async function signedIn(page: Page, who: { email: string; password: string }, path: string) {
  const s = await login(who.email, who.password)
  await page.goto(`${base}/login`)
  await page.evaluate(({ a, r }) => { localStorage.setItem('foundu.accessToken', a); localStorage.setItem('foundu.refreshToken', r) }, { a: s.accessToken, r: s.refreshToken })
  await page.goto(`${base}${path}`)
}

test('CSP-01 public pages and Google sign-in load with no violation', async ({ page }) => {
  const violations = await watch(page)
  for (const path of ['/', '/feed', '/found', '/register', '/login']) {
    await page.goto(`${base}${path}`)
    await page.waitForLoadState('networkidle')
  }
  // Google's sign-in button is a script and an iframe from accounts.google.com.
  await expect(page.locator('iframe[src*="accounts.google.com/gsi"]').first()).toBeAttached({ timeout: 15_000 })
  expect(await violations()).toEqual([])
})

test('CSP-02 student pages, API calls and photos load with no violation', async ({ page }) => {
  const violations = await watch(page)
  await signedIn(page, STUDENT, '/my-reports')
  await expect(page.getByRole('heading').first()).toBeVisible()
  for (const path of ['/my-claims', '/ask-foundu', '/support', '/feed']) {
    await page.goto(`${base}${path}`)
    await page.waitForLoadState('networkidle')
  }
  expect(await violations()).toEqual([])
})

test('CSP-03 staff desk pages load with no violation', async ({ page }) => {
  const violations = await watch(page)
  await signedIn(page, STAFF, '/items')
  for (const path of ['/claims', '/handovers', '/items/new']) {
    await page.goto(`${base}${path}`)
    await page.waitForLoadState('networkidle')
  }
  expect(await violations()).toEqual([])
})

test('CSP-04 the site refuses to be framed (clickjacking)', async ({ request }) => {
  const res = await request.get(`${base}/login`)
  expect(res.headers()['x-frame-options']).toBe('DENY')
  expect(res.headers()['content-security-policy']).toContain("frame-ancestors 'none'")
  expect(res.headers()['strict-transport-security']).toContain('max-age=')
})
