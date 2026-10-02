import { expect, test, type Page } from '@playwright/test'
import process from 'node:process'

const demoPassword = process.env.FOUNDU_E2E_PASSWORD ?? 'Demo!Pass2026'

const accounts = {
  student: {
    email: process.env.FOUNDU_E2E_STUDENT_EMAIL ?? 'amara@foundu.test',
    password: process.env.FOUNDU_E2E_STUDENT_PASSWORD ?? demoPassword,
    home: '/my-reports',
  },
  staff: {
    email: process.env.FOUNDU_E2E_STAFF_EMAIL ?? 'priya@foundu.test',
    password: process.env.FOUNDU_E2E_STAFF_PASSWORD ?? demoPassword,
    home: '/items',
  },
  admin: {
    email: process.env.FOUNDU_E2E_ADMIN_EMAIL ?? 'admin@foundu.com',
    password:
      process.env.FOUNDU_E2E_ADMIN_PASSWORD ??
      process.env.DEV_ADMIN_PASSWORD ??
      process.env.FOUNDU_ADMIN_PASSWORD ??
      '',
    home: '/admin',
  },
} as const

async function signIn(page: Page, email: string, password: string) {
  await page.goto('/login')
  await page.getByLabel('Email address').fill(email)
  await page.getByRole('textbox', { name: 'Password' }).fill(password)
  await page.getByRole('button', { name: 'Sign in' }).click()
}

async function chooseFirstOption(page: Page, controlId: string) {
  await page.locator(`#${controlId}`).click()
  await page.getByRole('option').first().click()
}

async function selectToday(page: Page, controlId: string) {
  const today = await page.evaluate(() => new Date().toLocaleDateString('en'))
  await page.locator(`#${controlId}`).click()
  await page.locator(`[data-day="${today}"]`).click()
}

async function openLostReportWizard(page: Page) {
  await page.goto('/my-reports/new')
  await expect(page.getByRole('heading', { name: 'What did you lose?' })).toBeVisible()
}

function captureBrowserFailures(page: Page) {
  const consoleErrors: string[] = []
  const apiFailures: string[] = []
  const apiOrigin = new URL(process.env.VITE_API_BASE_URL ?? 'http://localhost:5292').origin

  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text())
  })
  page.on('pageerror', (error) => consoleErrors.push(error.message))
  page.on('requestfailed', (request) => {
    if (new URL(request.url()).origin === apiOrigin) {
      apiFailures.push(`${request.method()} ${new URL(request.url()).pathname}: ${request.failure()?.errorText}`)
    }
  })
  page.on('response', (response) => {
    const url = new URL(response.url())
    if (url.origin === apiOrigin && response.status() >= 400) {
      apiFailures.push(`${response.status()} ${url.pathname}`)
    }
  })

  return { consoleErrors, apiFailures }
}

test.describe('public visitor journeys', () => {
  test('visitor can reach public feeds and is redirected from a protected page', async ({ page }) => {
    for (const route of ['/', '/feed', '/found']) {
      await page.goto(route)
      await expect(page.locator('body')).not.toContainText('Page not found')
      await expect(page).toHaveURL(new RegExp(`${route === '/' ? '/$' : `${route}$`}`))
    }

    await page.goto('/my-reports')
    await expect(page).toHaveURL(/\/login$/)
    await expect(page.getByRole('heading', { name: 'Welcome back' })).toBeVisible()
  })

  test('incorrect credentials are rejected without disclosing account state', async ({ page }) => {
    await signIn(page, accounts.student.email, 'Wrong!Pass2026')
    await expect(page.getByText('Incorrect email or password.')).toBeVisible()
    await expect(page).toHaveURL(/\/login$/)
  })

  test('duplicate registration is rejected with a sign-in path', async ({ page }) => {
    await page.goto('/register')
    await page.getByLabel('Full name').fill('E2E Duplicate')
    await page.getByLabel('Email address').fill(accounts.student.email)
    await page.getByRole('textbox', { name: 'Password' }).fill(demoPassword)
    await page.getByRole('button', { name: 'Create account' }).click()

    await expect(page.getByText('An account with this email already exists. Try signing in instead.')).toBeVisible()
    await expect(page).toHaveURL(/\/register$/)
  })
})

test.describe('role-based browser access', () => {
  for (const [role, account] of Object.entries(accounts)) {
    test(`${role} signs in and lands on the correct home page`, async ({ page }) => {
      test.skip(!account.password, 'Set FOUNDU_E2E_ADMIN_PASSWORD or DEV_ADMIN_PASSWORD for admin browser tests.')

      await signIn(page, account.email, account.password)
      await expect(page).toHaveURL(new RegExp(`${account.home.replaceAll('/', '\\/')}$`))
    })
  }

  test('student and staff are sent to Forbidden for routes outside their role', async ({ page }) => {
    await signIn(page, accounts.student.email, accounts.student.password)
    await expect(page).toHaveURL(/\/my-reports$/)
    await page.goto('/items')
    await expect(page).toHaveURL(/\/forbidden$/)

    await page.evaluate(() => localStorage.clear())
    await page.reload()
    await signIn(page, accounts.staff.email, accounts.staff.password)
    await expect(page).toHaveURL(/\/items$/)
    await page.goto('/admin/analytics')
    await expect(page).toHaveURL(/\/forbidden$/)
  })

  const sharedRoutes = [
    '/account',
    '/support',
    '/found/new',
    '/feed',
    '/found',
    ...(process.env.FOUNDU_E2E_CLAIM_ID ? [`/claims/${process.env.FOUNDU_E2E_CLAIM_ID}`] : []),
  ]
  const staffRoutes = [
    '/items',
    '/items/new',
    '/claims',
    '/handovers',
    '/admin/support',
    '/admin/overview',
    ...(process.env.FOUNDU_E2E_ITEM_ID ? [`/items/${process.env.FOUNDU_E2E_ITEM_ID}`] : []),
  ]
  const routeMatrix = {
    student: [...sharedRoutes, '/my-reports', '/my-reports/new', '/my-claims', '/ask-foundu', '/help-to-find'],
    staff: [...sharedRoutes, ...staffRoutes],
    admin: [...sharedRoutes, ...staffRoutes, '/admin', '/admin/reference', '/admin/analytics', '/admin/moderation'],
  }

  for (const [role, paths] of Object.entries(routeMatrix)) {
    const account = accounts[role as keyof typeof accounts]
    test(`${role} can open each assigned web route`, async ({ page }) => {
      test.skip(!account.password, 'Set FOUNDU_E2E_ADMIN_PASSWORD or DEV_ADMIN_PASSWORD for admin browser tests.')
      const { consoleErrors, apiFailures } = captureBrowserFailures(page)

      await signIn(page, account.email, account.password)
      await expect(page).toHaveURL(new RegExp(`${account.home.replaceAll('/', '\\/')}$`))

      for (const route of paths) {
        await page.goto(route)
        await expect(page).toHaveURL(new RegExp(`${route.replaceAll('/', '\\/')}$`))
        await expect(page).not.toHaveURL(/\/forbidden$/)
        await expect(page).not.toHaveURL(/\/not-found$/)
      }

      expect(consoleErrors).toEqual([])
      expect(apiFailures).toEqual([])
    })
  }
})

test.describe('lost report form boundaries', () => {
  test.beforeEach(async ({ page }) => {
    await signIn(page, accounts.student.email, accounts.student.password)
    await expect(page).toHaveURL(/\/my-reports$/)
    await openLostReportWizard(page)
  })

  test('requires a category and item type before advancing', async ({ page }) => {
    await expect(page.getByRole('button', { name: 'Continue' })).toBeDisabled()
    await chooseFirstOption(page, 'category')
    await expect(page.getByRole('button', { name: 'Continue' })).toBeEnabled()
  })

  test('rejects a reversed time window and only enables the next step after correction', async ({ page }) => {
    await chooseFirstOption(page, 'category')
    await chooseFirstOption(page, 'itemType')
    await page.getByRole('button', { name: 'Continue' }).click()
    await chooseFirstOption(page, 'location')
    await selectToday(page, 'from')
    await selectToday(page, 'to')

    await page.locator('#from-hour').click()
    await page.getByRole('option', { name: '22', exact: true }).click()
    await page.locator('#to-hour').click()
    await page.getByRole('option', { name: '21', exact: true }).click()

    await expect(page.getByText('The end of the window must be after the start.')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Continue' })).toBeDisabled()

    await page.locator('#to-hour').click()
    await page.getByRole('option', { name: '23', exact: true }).click()
    await expect(page.getByRole('button', { name: 'Continue' })).toBeEnabled()
  })

  test('prevents selecting a future date for the lost-item window', async ({ page }) => {
    await chooseFirstOption(page, 'category')
    await chooseFirstOption(page, 'itemType')
    await page.getByRole('button', { name: 'Continue' }).click()

    await page.locator('#from').click()
    const futureDateLabel = await page.evaluate(() => {
      const tomorrow = new Date()
      tomorrow.setDate(tomorrow.getDate() + 1)
      return tomorrow.toLocaleDateString('en')
    })

    await expect(page.locator(`[data-day="${futureDateLabel}"]`)).toBeDisabled()
  })

  test('accepts a 10-character description and rejects 9 characters at the UI boundary', async ({ page }) => {
    await chooseFirstOption(page, 'category')
    await chooseFirstOption(page, 'itemType')
    await page.getByRole('button', { name: 'Continue' }).click()
    await chooseFirstOption(page, 'location')
    await page.getByRole('button', { name: 'Continue' }).click()

    const description = page.getByLabel('Description')
    await description.fill('123456789')
    await expect(page.getByText('1 more character needed')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Continue' })).toBeDisabled()

    await description.fill('1234567890')
    await expect(page.getByText('10 characters')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Continue' })).toBeEnabled()
  })

  test('API accepts a 1000-character description and rejects 1001 characters', async ({ page }) => {
    await chooseFirstOption(page, 'category')
    await chooseFirstOption(page, 'itemType')
    await page.getByRole('button', { name: 'Continue' }).click()
    await chooseFirstOption(page, 'location')
    await page.getByRole('button', { name: 'Continue' }).click()
    const description = page.getByLabel('Description')

    const atLimit = `E2E ${Date.now()} ${'x'.repeat(1000)}`.slice(0, 1000)
    await description.fill(atLimit)
    await expect(page.getByText('1000 characters')).toBeVisible()
    await page.getByRole('button', { name: 'Continue' }).click()
    await expect(page.getByRole('heading', { name: 'Ready to post?' })).toBeVisible()
    await page.getByRole('button', { name: 'Post report' }).click()
    await expect(page).toHaveURL(/\/my-reports$/)
    await expect(page.getByText('Report posted. We will tell you if something matching turns up.')).toBeVisible()

    await openLostReportWizard(page)
    await chooseFirstOption(page, 'category')
    await chooseFirstOption(page, 'itemType')
    await page.getByRole('button', { name: 'Continue' }).click()
    await chooseFirstOption(page, 'location')
    await page.getByRole('button', { name: 'Continue' }).click()
    await page.getByLabel('Description').fill(`${'x'.repeat(1000)}y`)
    await page.getByRole('button', { name: 'Continue' }).click()
    await page.getByRole('button', { name: 'Post report' }).click()

    await expect(page.getByLabel('Description')).toHaveAttribute('aria-invalid', 'true')
    await expect(page).toHaveURL(/\/my-reports\/new$/)
  })
})