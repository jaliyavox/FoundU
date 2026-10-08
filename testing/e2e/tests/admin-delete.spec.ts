import { expect, test } from '@playwright/test'
import { API, STAFF, call, login, registerStudent, signInAs } from './support/api'

/**
 * E2E-ADMIN: an administrator deleting a support ticket and a student account through the
 * admin pages. Needs the local admin's password: FOUNDU_ADMIN_PASSWORD=... npx playwright test
 */
const ADMIN = { email: process.env.FOUNDU_ADMIN_EMAIL ?? 'admin@foundu.com', password: process.env.FOUNDU_ADMIN_PASSWORD ?? '' }
test.skip(!ADMIN.password, 'set FOUNDU_ADMIN_PASSWORD to run the admin specs')

test('E2E-ADMIN-01 an admin deletes a spam ticket; it leaves the queue and the student\'s list', async ({ page }) => {
  const student = await registerStudent('Ticket')
  const ticket = await call('POST', '/api/support/tickets', student.accessToken, {
    subject: `Spam ${student.email}`, category: 'Other', body: 'Buy cheap watches at example dot com today', relatedEntityType: null, relatedEntityId: null,
  })

  await signInAs(page, await login(ADMIN.email, ADMIN.password), '/admin/support')
  await page.getByText(`Spam ${student.email}`).click()
  await page.getByRole('button', { name: 'Delete ticket' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Delete ticket' }).click()
  await expect(page.getByText('Ticket deleted.')).toBeVisible()
  await expect(page.getByText(`Spam ${student.email}`)).toHaveCount(0)

  const mine = await call('GET', '/api/support/tickets', student.accessToken)
  expect(mine.items.map((t: { id: string }) => t.id)).not.toContain(ticket.id)
})

test('E2E-ADMIN-02 staff can answer tickets but are not offered, or allowed, deletion', async ({ page }) => {
  const student = await registerStudent('Ticket')
  const ticket = await call('POST', '/api/support/tickets', student.accessToken, {
    subject: `Keep ${student.email}`, category: 'Other', body: 'Where do I collect my wallet from please?', relatedEntityType: null, relatedEntityId: null,
  })
  const staff = await login(STAFF.email, STAFF.password)
  await signInAs(page, staff, '/admin/support')
  await page.getByText(`Keep ${student.email}`).click()
  await expect(page.getByRole('button', { name: 'Assign to me' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Delete ticket' })).toHaveCount(0)
  const res = await fetch(`${API}/api/admin/support/tickets/${ticket.id}`, { method: 'DELETE', headers: { Authorization: `Bearer ${staff.accessToken}` } })
  expect(res.status).toBe(403)
})

test('E2E-ADMIN-03 an admin deletes a student: signed out, erased, and free to register again', async ({ page }) => {
  const student = await registerStudent('Doomed')

  await signInAs(page, await login(ADMIN.email, ADMIN.password), '/admin')
  await page.locator('#user-search').fill(student.email)
  await page.keyboard.press('Enter')
  await page.getByRole('button', { name: `Delete ${student.fullName}` }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Delete account' }).click()
  await expect(page.getByText(`${student.fullName}'s account has been deleted.`)).toBeVisible()
  await expect(page.getByRole('button', { name: `Delete ${student.fullName}` })).toHaveCount(0)

  // Every session ends: the old access token, the refresh token and the password all fail.
  await call('GET', '/api/auth/me', student.accessToken, undefined, [401])
  await call('POST', '/api/auth/refresh', undefined, { refreshToken: student.refreshToken }, [400, 401])
  await call('POST', '/api/auth/login', undefined, { email: student.email, password: student.password }, [401])
  // The details were erased, so the same person can sign up again.
  await call('POST', '/api/auth/register', undefined, { fullName: student.fullName, email: student.email, password: student.password, studentNumber: student.studentNumber })
})

test('E2E-ADMIN-04 an admin cannot delete their own account', async ({ page }) => {
  await signInAs(page, await login(ADMIN.email, ADMIN.password), '/admin')
  await page.locator('#user-search').fill(ADMIN.email)
  await page.keyboard.press('Enter')
  const own = page.getByRole('button', { name: /^Delete / }).first()
  await expect(own).toBeDisabled()
})
