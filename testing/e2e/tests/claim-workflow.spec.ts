import { expect, test } from '@playwright/test'
import { STAFF, call, createLostReport, logFoundItem, login, referenceData, registerStudent, run, signInAs } from './support/api'

/**
 * E2E-WF-01: the complete business workflow, across every component.
 * React (staff and owner) -> ASP.NET API -> AI service (matching agent) -> PostgreSQL, and back.
 * Lost report -> item logged at the desk -> AI match -> claim -> verification question ->
 * answer -> staff approval -> collection code -> desk hand-over -> owner told it was collected.
 */
test('E2E-WF-01 lost item to collected: match, claim, verify, approve and collect', async ({ browser }) => {
  test.setTimeout(180_000)
  const tag = run()
  const owner = await registerStudent('Owner')
  const staff = await login(STAFF.email, STAFF.password)
  const ref = await referenceData(staff.accessToken)
  const lost = await createLostReport(owner.accessToken, ref, `Purple wallet ${tag}, lost near the library, has a train pass`)
  const found = await logFoundItem(staff.accessToken, ref, `Purple wallet ${tag} handed in at the library`, 'Train pass inside with the name Claire')

  const staffPage = await (await browser.newContext()).newPage()
  const ownerPage = await (await browser.newContext()).newPage()

  await test.step('staff ask the matching agent whether the item fits the lost report', async () => {
    await signInAs(staffPage, staff, `/items/${found.id}`)
    await staffPage.getByRole('button', { name: 'Suggest to a report' }).click()
    await staffPage.getByLabel('Search active lost reports').fill(tag)
    await staffPage.getByRole('button', { name: 'Search', exact: true }).click()
    await staffPage.getByRole('button', { name: new RegExp(`Purple wallet ${tag}`) }).click()
    await staffPage.getByRole('button', { name: 'Generate AI Match Suggestion' }).click()
    await expect(staffPage.getByText(/Staff still decide any claim/)).toBeVisible({ timeout: 30_000 })
  })

  let claimId = ''
  await test.step('the owner sees the item as "might be yours" and claims it', async () => {
    await signInAs(ownerPage, owner, '/my-reports')
    await expect(ownerPage.getByText(`Purple wallet ${tag} handed in at the library`)).toBeVisible()
    await ownerPage.getByRole('button', { name: 'Yes, submit a claim' }).click()
    await expect(ownerPage).toHaveURL(/\/claims\/[0-9a-f-]{36}$/)
    claimId = ownerPage.url().split('/').pop()!
    // The hidden detail is the whole basis of verification: the claimant must never see it.
    await expect(ownerPage.getByText('Train pass inside with the name Claire')).toHaveCount(0)
    const asOwner = await call('GET', `/api/claims/${claimId}`, owner.accessToken)
    expect(JSON.stringify(asOwner)).not.toContain('Train pass inside')
  })

  await test.step('staff send a verification question', async () => {
    await staffPage.goto(`/claims/${claimId}`)
    await expect(staffPage.getByText('Train pass inside with the name Claire')).toBeVisible()
    await staffPage.getByPlaceholder(/front pocket|question/i).first().fill('What is inside the wallet?')
    await staffPage.getByRole('button', { name: 'Send to the claimant' }).click()
    await expect(staffPage.getByText('What is inside the wallet?')).toBeVisible()
  })

  await test.step('the owner answers from memory', async () => {
    await ownerPage.goto(`/claims/${claimId}`)
    await expect(ownerPage.getByText('What is inside the wallet?')).toBeVisible()
    await ownerPage.locator('textarea').first().fill('A train pass with my name, Claire')
    await ownerPage.getByRole('button', { name: 'Send my answers' }).click()
    await expect(ownerPage.getByText('A train pass with my name, Claire')).toBeVisible()
  })

  await test.step('staff compare the answer with the hidden detail and approve', async () => {
    await staffPage.reload()
    await expect(staffPage.getByText('A train pass with my name, Claire')).toBeVisible()
    await staffPage.getByRole('button', { name: 'Approve', exact: true }).click()
    await expect(staffPage.getByText(/approved/i).first()).toBeVisible()
  })

  let code = ''
  await test.step('the owner receives a six-digit collection code that staff never see', async () => {
    await ownerPage.reload()
    const card = ownerPage.locator('section', { hasText: 'Your collection code' }).last()
    await expect(card).toContainText(/\d/)
    code = (await card.innerText()).replace(/\D/g, '')
    expect(code).toHaveLength(6)
    const asStaff = await call('GET', `/api/claims/${claimId}`, staff.accessToken)
    expect(asStaff.collectionCode).toBeNull()
  })

  await test.step('the desk finds the item by code, checks ID and hands it over', async () => {
    await staffPage.goto('/claims')
    await staffPage.locator('#collect-code').fill(code)
    await staffPage.getByRole('button', { name: 'Find the item' }).click()
    await staffPage.getByRole('checkbox').first().check()
    await staffPage.getByRole('button', { name: 'Hand it over' }).click()
    await expect(staffPage.getByText(/handed|collected/i).first()).toBeVisible()
  })

  await test.step('the owner sees it collected and gets a receipt notification', async () => {
    await ownerPage.goto('/my-claims')
    await expect(ownerPage.locator('main')).toContainText('Collected')
    const notes = await call('GET', '/api/notifications', owner.accessToken)
    expect(notes.items.map((n: { type: string }) => n.type)).toContain('ItemCollected')
    expect((await call('GET', `/api/lost-reports/${lost.id}`, owner.accessToken)).status).toBe('Resolved')
    expect((await call('GET', `/api/found-reports/${found.id}`, staff.accessToken)).status).toBe('Returned')
  })

  await test.step('the code works once: a second hand-over is refused', async () => {
    await call('POST', '/api/claims/collect', staff.accessToken, { code, ownerIdChecked: true }, [404])
  })
})
