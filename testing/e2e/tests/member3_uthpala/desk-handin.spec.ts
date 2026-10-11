import { expect, test } from '@playwright/test'
import { STAFF, call, createLostReport, login, referenceData, registerStudent, run, signInAs } from '../support/api'

/** E2E-DESK: a finder brings an item to the security desk with a code; staff pull it up by that code. */
test.describe('Security desk hand-in by code', () => {
  test('E2E-DESK-01 a posted found item is pulled up by its post code and confirmed into storage', async ({ page }) => {
    const tag = run()
    const finder = await registerStudent('Finder')
    const staff = await login(STAFF.email, STAFF.password)
    const ref = await referenceData(staff.accessToken)
    const post = await call('POST', '/api/found-posts', finder.accessToken, {
      categoryId: ref.category.id,
      itemTypeId: ref.itemType.id,
      foundLocationId: ref.location.id,
      description: `Red item ${tag} found by the stairs`,
      primaryColor: 'Red',
      foundAt: new Date(Date.now() - 40 * 60_000).toISOString(),
    })
    expect(post.handInCode).toBeTruthy()
    await call('POST', `/api/found-posts/${post.id}/hand-in`, finder.accessToken)

    await signInAs(page, staff, '/items')
    await page.locator('#post-code').fill(post.handInCode)
    await page.getByRole('button', { name: 'Pull it up' }).click()
    await expect(page).toHaveURL(new RegExp(`/items/${post.id}`))
    await expect(page.getByText(`Red item ${tag} found by the stairs`).first()).toBeVisible()

    await page.locator('#confirm-storage').click()
    await page.getByRole('option').first().click()
    await page.locator('#confirm-private').fill('Name written inside the flap')
    await page.getByRole('button', { name: 'I received it at the security desk' }).click()
    await expect(page.getByText('Confirmed. It is in storage and can be claimed now.')).toBeVisible()

    const mine = await call('GET', '/api/found-posts/mine?pageSize=20', finder.accessToken)
    expect(mine.items.find((p: { id: string }) => p.id === post.id).status).toBe('Unclaimed')
    // The hidden detail stays with staff: the finder's own view of the post never carries it.
    expect(JSON.stringify(mine)).not.toContain('Name written inside the flap')
  })

  test('E2E-DESK-02 a handover code from "I found this" is taken in at the desk and the owner is told', async ({ page }) => {
    const tag = run()
    const owner = await registerStudent('Owner')
    const finder = await registerStudent('Finder')
    const staff = await login(STAFF.email, STAFF.password)
    const ref = await referenceData(staff.accessToken)
    const report = await createLostReport(owner.accessToken, ref, `Green item ${tag}, lost in the gym`, 'Green')
    await call('POST', `/api/lost-reports/${report.id}/found-claims`, finder.accessToken)
    const { code } = await call('POST', `/api/lost-reports/${report.id}/handover`, finder.accessToken)

    await signInAs(page, staff, '/items')
    await page.locator('#post-code').fill(code)
    await page.getByRole('button', { name: 'Pull it up' }).click()
    await expect(page).toHaveURL(/\/handovers/)
    await page.locator('#handover-storage').click()
    await page.getByRole('option').first().click()
    await page.getByRole('button', { name: 'Take the item in' }).click()
    await expect(page.getByText('Logged. The owner has been told where it is.')).toBeVisible()

    const ownerView = await call('GET', `/api/lost-reports/${report.id}/handover`, owner.accessToken)
    expect(ownerView.status).toBe('InCustody')
  })

  test('E2E-DESK-03 the code box takes exactly six digits: letters are dropped, five digits cannot be sent', async ({ page }) => {
    await signInAs(page, await login(STAFF.email, STAFF.password), '/items')
    const box = page.locator('#post-code')
    const pull = page.getByRole('button', { name: 'Pull it up' })
    await box.fill('AB12345')
    await expect(box).toHaveValue('12345')
    await expect(pull).toBeDisabled() // 5 digits: one under the boundary
    await box.fill('123456')
    await expect(pull).toBeEnabled() // exactly 6
  })

  test('E2E-DESK-04 an unknown six-digit code is reported clearly and the desk stays put', async ({ page }) => {
    await signInAs(page, await login(STAFF.email, STAFF.password), '/items')
    await page.locator('#post-code').fill('000000')
    await page.getByRole('button', { name: 'Pull it up' }).click()
    await expect(page.locator('[data-sonner-toast]').first()).toBeVisible()
    await expect(page).toHaveURL(/\/items$/)
  })
})
