import { expect, test } from '@playwright/test'
import { STAFF, call, logFoundItem, login, referenceData, registerStudent, run, signInAs } from '../support/api'

/** E2E-DESK-05: the owner is at the desk with no report - staff verify in person and hand over. */
test('E2E-DESK-05 staff verify an owner in person and hand the item over in one step', async ({ page }) => {
  const tag = run()
  const staff = await login(STAFF.email, STAFF.password)
  const ref = await referenceData(staff.accessToken)
  const owner = await registerStudent('Walkin')
  const item = await logFoundItem(staff.accessToken, ref, `Blue umbrella ${tag}`, 'Initials J.K. on the handle')

  await signInAs(page, staff, `/items/${item.id}`)
  await page.getByRole('button', { name: 'Owner here in person' }).click()
  await page.locator('#desk-student').fill(owner.email)
  await page.getByRole('button', { name: 'Find', exact: true }).click()
  await page.getByRole('button', { name: new RegExp(owner.fullName) }).click()
  await page.locator('#desk-notes').fill('Asked what is on the handle - said initials J.K. Correct.')
  const handOver = page.getByRole('button', { name: 'Verify and hand over' })
  await expect(handOver).toBeDisabled()            // not before the ID check
  await page.getByRole('checkbox').check()
  await handOver.click()
  await expect(page.getByText(`Handed over to ${owner.fullName}`)).toBeVisible()

  expect((await call('GET', `/api/found-reports/${item.id}`, staff.accessToken)).status).toBe('Returned')
  const notes = await call('GET', '/api/notifications', owner.accessToken)
  expect(notes.items.map((n: { type: string }) => n.type)).toContain('ItemCollected')
  const claims = await call('GET', '/api/claims/mine', owner.accessToken)
  expect(claims.items[0].collectedAt).toBeTruthy()
})
