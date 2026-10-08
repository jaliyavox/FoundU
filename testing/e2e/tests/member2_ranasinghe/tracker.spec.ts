import { expect, test } from '@playwright/test'
import { STAFF, call, createLostReport, login, referenceData, registerStudent, signInAs } from '../support/api'

/** E2E-REG-03: the owner's report tracker follows a finder's hand-in to the desk. */
test('E2E-REG-03 the report tracker moves when a finder hands the item in', async ({ page }) => {
  const owner = await registerStudent('Owner')
  const finder = await registerStudent('Finder')
  const staff = await login(STAFF.email, STAFF.password)
  const ref = await referenceData(staff.accessToken)
  const report = await createLostReport(owner.accessToken, ref, 'Tracker test: grey bottle with a dent', 'Grey')

  await signInAs(page, owner, '/my-reports')
  await expect(page.getByText('now', { exact: true }).locator('..')).toContainText('Reported')

  await call('POST', `/api/lost-reports/${report.id}/found-claims`, finder.accessToken)
  await page.reload()
  await expect(page.getByText('now', { exact: true }).locator('..')).toContainText('Finder Has It')

  const { code } = await call('POST', `/api/lost-reports/${report.id}/handover`, finder.accessToken)
  await page.reload()
  await expect(page.getByText('now', { exact: true }).locator('..')).toContainText('On Its Way to the Desk')

  await call('POST', `/api/handovers/by-code/${code}/receive`, staff.accessToken, { storageLocationId: ref.storage.id, note: null })
  await page.reload()
  await expect(page.getByText('now', { exact: true }).locator('..')).toContainText('At the Desk')
  await expect(page.getByText(/At the security desk - collect it/)).toBeVisible()
})
