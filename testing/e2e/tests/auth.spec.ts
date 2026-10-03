import { expect, test } from '@playwright/test'
import { STAFF, STUDENT, call, login, run, signInAs } from './support/api'

/** E2E-AUTH: sign-in, registration and the route guards, through the real pages and API. */
test.describe('Authentication and protected routes', () => {
  test('E2E-AUTH-01 a student signs in through the form and lands on My reports', async ({ page }) => {
    await page.goto('/login')
    await page.getByLabel('Email address').fill(STUDENT.email)
    await page.locator('#password').fill(STUDENT.password)
    await page.getByRole('button', { name: 'Sign in', exact: true }).click()
    await expect(page).toHaveURL(/\/my-reports$/)
  })

  test('E2E-AUTH-02 staff sign in and land on the found-items desk', async ({ page }) => {
    await page.goto('/login')
    await page.getByLabel('Email address').fill(STAFF.email)
    await page.locator('#password').fill(STAFF.password)
    await page.getByRole('button', { name: 'Sign in', exact: true }).click()
    await expect(page).toHaveURL(/\/items$/)
  })

  test('E2E-AUTH-03 a wrong password is refused and the user stays on the sign-in page', async ({ page }) => {
    await page.goto('/login')
    await page.getByLabel('Email address').fill(STUDENT.email)
    await page.locator('#password').fill('Wrong-password-1')
    await page.getByRole('button', { name: 'Sign in', exact: true }).click()
    await expect(page.getByText('Incorrect email or password.')).toBeVisible()
    await expect(page).toHaveURL(/\/login$/)
  })

  test('E2E-AUTH-04 an empty sign-in form shows field errors from the API', async ({ page }) => {
    await page.goto('/login')
    await page.getByRole('button', { name: 'Sign in', exact: true }).click()
    await expect(page.locator('#email-error')).toBeVisible()
    await expect(page).toHaveURL(/\/login$/)
  })

  test('E2E-AUTH-05 registration refuses a 7-character password (boundary) and accepts a valid one', async ({ page }) => {
    const id = run()
    await page.goto('/register')
    await page.locator('#fullName').fill(`E2E Register ${id}`)
    await page.locator('#email').fill(`e2e-register-${id}@foundu.test`)
    await page.locator('#studentNumber').fill(`IT26${id}`)
    await page.locator('#password').fill('Abcde1x') // 7 characters: one under the minimum
    await page.getByRole('button', { name: 'Create account' }).click()
    await expect(page).toHaveURL(/\/register$/)
    await expect(page.locator('#password').locator('xpath=ancestor::form')).toContainText(/at least 8|8 characters/i)

    await page.locator('#password').fill('Abcde1xy') // exactly 8: the minimum
    await page.getByRole('button', { name: 'Create account' }).click()
    await expect(page).toHaveURL(/\/my-reports$/)
    await expect(page.getByText(/Welcome to FoundU/)).toBeVisible()
  })

  test('E2E-AUTH-06 registering an email that already exists is refused with a clear message', async ({ page }) => {
    await page.goto('/register')
    await page.locator('#fullName').fill('Duplicate Amara')
    await page.locator('#email').fill(STUDENT.email)
    await page.locator('#password').fill('Abcdefg1')
    await page.getByRole('button', { name: 'Create account' }).click()
    await expect(page.getByText(/already exists/i)).toBeVisible()
  })

  test('E2E-AUTH-07 an anonymous visitor is sent to sign in from a protected page', async ({ page }) => {
    await page.goto('/my-claims')
    await expect(page).toHaveURL(/\/login$/)
  })

  test('E2E-AUTH-11 after signing in, the student is taken back to the page they were trying to open', async ({ page }) => {
    await page.goto('/my-claims')
    await expect(page).toHaveURL(/\/login$/)
    await page.getByLabel('Email address').fill(STUDENT.email)
    await page.locator('#password').fill(STUDENT.password)
    await page.getByRole('button', { name: 'Sign in', exact: true }).click()
    await expect(page).toHaveURL(/\/my-claims$/)
  })

  test('E2E-AUTH-08 a student cannot open the staff desk or the admin area', async ({ page }) => {
    await signInAs(page, await login(STUDENT.email, STUDENT.password), '/claims')
    await expect(page).toHaveURL(/\/forbidden$/)
    await page.goto('/admin/overview')
    await expect(page).toHaveURL(/\/forbidden$/)
  })

  test('E2E-AUTH-09 staff cannot open admin-only pages', async ({ page }) => {
    await signInAs(page, await login(STAFF.email, STAFF.password), '/admin/moderation')
    await expect(page).toHaveURL(/\/forbidden$/)
  })

  test('E2E-AUTH-10 signing out ends the session and revokes the refresh token', async ({ page }) => {
    const session = await login(STUDENT.email, STUDENT.password)
    await signInAs(page, session, '/my-reports')
    await page.getByRole('button', { name: /Student/ }).filter({ hasText: STUDENT.email.split('@')[0].replace(/^./, (c) => c.toUpperCase()) }).first().click()
    await page.getByRole('menuitem', { name: 'Sign out' }).click()
    await expect(page).toHaveURL(/\/login$/)
    await page.goto('/my-claims')
    await expect(page).toHaveURL(/\/login$/)
    const refresh = await call('POST', '/api/auth/refresh', undefined, { refreshToken: session.refreshToken }, [400, 401])
    expect(refresh).not.toHaveProperty('accessToken')
  })
})
