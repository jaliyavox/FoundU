import { expect, test } from '@playwright/test'
import { STAFF, STUDENT, login, signInAs } from './support/api'

/** Retests for defects found while testing the deployment and in this pass (defect report 48, 49, 54). */

test('E2E-REG-01 (D49, D54) a question asked before signing in is answered after sign-in - no stuck spinner, not lost', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('button', { name: 'Ask FoundU about something you lost or found' }).click()
  await page.getByLabel('What did you lose or find?').fill('I lost my black wallet in the library')
  await page.getByRole('button', { name: 'Ask', exact: true }).click()

  // Signing in carries the sentence to Ask FoundU, which sends it on arrival.
  await expect(page).toHaveURL(/\/login$/)
  await page.getByLabel('Email address').fill(STUDENT.email)
  await page.locator('#password').fill(STUDENT.password)
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  await expect(page).toHaveURL(/\/ask-foundu$/)

  const log = page.getByRole('log', { name: 'Conversation with FoundU' })
  await expect(log).toContainText('I lost my black wallet in the library')
  await expect(page.getByText('Checking your details…')).toHaveCount(0, { timeout: 30_000 })
  // Greeting, the question, then FoundU's reply - and the draft it built from the sentence.
  await expect(log.locator('li')).toHaveCount(3, { timeout: 30_000 })
  await expect(log.locator('li').last()).toContainText('FoundU')
  await expect(page.getByText('Your report draft')).toBeVisible()
})

test('E2E-REG-02 (D48) logging an item with nothing chosen names the missing fields instead of a 400', async ({ page }) => {
  const badRequests: string[] = []
  page.on('response', (r) => { if (r.url().includes('/api/found-reports') && r.status() === 400) badRequests.push(r.url()) })
  await signInAs(page, await login(STAFF.email, STAFF.password), '/items/new')
  await page.locator('#description').fill('Grey umbrella with a wooden handle') // the browser requires this one
  await page.getByRole('button', { name: 'Log the item' }).click()

  await expect(page.getByText('A few details are missing - see the highlighted fields.')).toBeVisible()
  await expect(page.getByText('Choose a category.')).toBeVisible()
  await expect(page.getByText('Choose where it is being kept.')).toBeVisible()
  expect(badRequests).toEqual([]) // refused in the browser; nothing invalid reached the API
})
