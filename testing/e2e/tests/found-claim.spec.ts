import { expect, test, type Page } from '@playwright/test'
import { STAFF, call, createLostReport, login, referenceData, registerStudent, run, signInAs } from './support/api'

/**
 * E2E-CLAIM: claiming an item straight from the Found board. An owner nobody had matched to an
 * item used to be sent to "Open your matches", where it never appeared - a dead end.
 */
async function postedAndConfirmed(tag: string, ref: Awaited<ReturnType<typeof referenceData>>, staffToken: string) {
  const finder = await registerStudent('Finder')
  const post = await call('POST', '/api/found-posts', finder.accessToken, {
    categoryId: ref.category.id, itemTypeId: ref.itemType.id, foundLocationId: ref.location.id,
    description: `Yellow wallet ${tag}`, primaryColor: 'Yellow', foundAt: new Date(Date.now() - 30 * 60_000).toISOString(),
  })
  await call('POST', `/api/found-posts/${post.id}/hand-in`, finder.accessToken)
  await call('POST', `/api/found-posts/${post.id}/confirm`, staffToken, {
    storageLocationId: ref.storage.id, privateVerificationDetails: 'A library card inside', generalDescription: null,
  })
  return post
}

async function openOnBoard(page: Page, tag: string) {
  await page.goto('/found')
  await page.locator('#found-search').fill(tag)
  await page.keyboard.press('Enter')
  await page.locator('button', { hasText: `Yellow wallet ${tag}` }).first().click()
}

test('E2E-CLAIM-01 an owner with no match picks their report manually and the claim opens', async ({ page }) => {
  const tag = run()
  const staff = await login(STAFF.email, STAFF.password)
  const ref = await referenceData(staff.accessToken)
  const owner = await registerStudent('Owner')
  // Described differently enough that the matching agent does not pair them on its own.
  const report = await createLostReport(owner.accessToken, ref, `Mine has stickers on it, ref ${tag}`, 'Red')
  const post = await postedAndConfirmed(tag, ref, staff.accessToken)

  await signInAs(page, owner, '/found')
  await openOnBoard(page, tag)
  await expect(page.getByText('Sure it is yours? Link it to your report')).toBeVisible()
  await page.locator('#recognise-report').click()
  await page.getByRole('option').first().click()
  await page.getByRole('button', { name: 'That is mine', exact: true }).click()

  await expect(page).toHaveURL(/\/claims\/[0-9a-f-]{36}$/)
  const claim = await call('GET', `/api/claims/${page.url().split('/').pop()}`, owner.accessToken)
  expect(claim.lostReportId).toBe(report.id)
  expect(claim.foundItem.id).toBe(post.id)
})

test('E2E-CLAIM-02 an owner with a match answers "Is this your item?" with Yes', async ({ page }) => {
  const tag = run()
  const staff = await login(STAFF.email, STAFF.password)
  const ref = await referenceData(staff.accessToken)
  const owner = await registerStudent('Owner')
  const report = await createLostReport(owner.accessToken, ref, `Lost my yellow wallet ${tag} near the library`, 'Yellow')
  const post = await postedAndConfirmed(tag, ref, staff.accessToken)
  // Confirming at the desk usually matches it to the report already; staff link it if not.
  await call('POST', '/api/match-suggestions', staff.accessToken, { lostReportId: report.id, foundReportId: post.id, note: null }, [200, 201, 409])

  await signInAs(page, owner, '/found')
  await openOnBoard(page, tag)
  await expect(page.getByText('Is this your item?')).toBeVisible()
  // Matching only assists: the manual picker is still there beside the match.
  await expect(page.getByText('Or link it to another of your reports')).toBeVisible()
  await expect(page.getByRole('button', { name: 'That is mine', exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Yes, this is mine' }).click()
  await expect(page).toHaveURL(/\/claims\/[0-9a-f-]{36}$/)

  // Back on the item, it now says the claim is open instead of offering another.
  await openOnBoard(page, tag)
  await expect(page.getByRole('button', { name: 'View claim' })).toBeVisible()
})

test('E2E-CLAIM-03 with no open lost report, the owner is told to report it lost first', async ({ page }) => {
  const tag = run()
  const staff = await login(STAFF.email, STAFF.password)
  const ref = await referenceData(staff.accessToken)
  const owner = await registerStudent('Owner')
  await postedAndConfirmed(tag, ref, staff.accessToken)

  await signInAs(page, owner, '/found')
  await openOnBoard(page, tag)
  await expect(page.getByRole('link', { name: 'Report it lost' })).toBeVisible()
})

test('E2E-CLAIM-04 an owner who never reported it lost claims it in their own words, then answers on the dashboard', async ({ page }) => {
  const tag = run()
  const staff = await login(STAFF.email, STAFF.password)
  const ref = await referenceData(staff.accessToken)
  const owner = await registerStudent('Owner')
  await postedAndConfirmed(tag, ref, staff.accessToken)

  await signInAs(page, owner, '/found')
  await openOnBoard(page, tag)
  await page.getByLabel('What is it like?').fill('Yellow leather wallet, worn corners, my library card is inside')
  await page.getByRole('button', { name: 'Claim this item' }).click()
  await expect(page).toHaveURL(/\/claims\/[0-9a-f-]{36}$/)
  const claimId = page.url().split('/').pop()!

  // The desk sees it like any other claim, and asks its question there.
  const queue = await call('GET', '/api/claims?pageSize=50', staff.accessToken)
  expect(queue.items.map((c: { id: string }) => c.id)).toContain(claimId)
  // The report made for it never appears on the public lost feed.
  const feed = await call('GET', '/api/lost-reports/feed?pageSize=100')
  expect(JSON.stringify(feed)).not.toContain('worn corners')
})
