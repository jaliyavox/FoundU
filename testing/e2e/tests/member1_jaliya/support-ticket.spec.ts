import { expect, test } from '@playwright/test'
import { STAFF, call, login, registerStudent, run, signInAs } from '../support/api'

/**
 * E2E-SUP-01: Help & support, end to end.
 * React (student) -> ASP.NET API -> AI service (Support agent) -> PostgreSQL -> React (staff).
 * The assistant answers a how-to question from the help guide; when that does not solve it,
 * it drafts a ticket the student checks and sends. Staff see it in the Support queue marked
 * as already tried by the assistant, reply, and the student reads the reply on their ticket.
 */
test('E2E-SUP-01 assistant answers, escalates a draft, staff reply in the queue, student sees it', async ({ browser }) => {
  test.setTimeout(120_000)
  const tag = run()
  const student = await registerStudent('Support')
  const staff = await login(STAFF.email, STAFF.password)
  const studentPage = await (await browser.newContext()).newPage()
  const staffPage = await (await browser.newContext()).newPage()
  const subject = `Collection code not accepted ${tag}`
  const reply = `Bring your student ID to the main desk, we will reissue the code (${tag}).`

  await test.step('the assistant answers a how-to question from the help guide', async () => {
    await signInAs(studentPage, student, '/support')
    await studentPage.getByLabel('Your question').fill('How do I collect my item with the collection code?')
    await studentPage.getByRole('button', { name: 'Ask', exact: true }).click()
    const conversation = studentPage.getByRole('log', { name: 'Conversation with the support assistant' })
    await expect(conversation).toContainText('Did that solve it?', { timeout: 30_000 })
  })

  await test.step('it did not help: the assistant drafts a ticket and the student sends it', async () => {
    await studentPage.getByRole('button', { name: 'No, I still need help' }).click()
    await expect(studentPage.getByText('A ticket for the support team')).toBeVisible({ timeout: 30_000 })
    // Nothing is sent for them: the draft is theirs to change first.
    await studentPage.getByLabel('Subject').fill(subject)
    await studentPage.getByRole('button', { name: 'Send to the support team' }).click()
    await expect(studentPage.getByRole('heading', { name: subject })).toBeVisible()
    await expect(studentPage.getByText('Assistant tried first')).toBeVisible()
  })

  await test.step('staff see it in the Support queue with the Assistant badge and reply', async () => {
    await signInAs(staffPage, staff, '/admin/support')
    const row = staffPage.getByRole('button', { name: new RegExp(subject) })
    await expect(row).toBeVisible()
    await expect(row.getByText('Assistant tried first')).toBeVisible()
    await row.scrollIntoViewIfNeeded()
    await test.info().attach('support queue with the Assistant badge', { body: await staffPage.screenshot(), contentType: 'image/png' })
    await row.click()
    await expect(staffPage.getByRole('heading', { name: subject })).toBeVisible()
    await staffPage.getByLabel('Your message').fill(reply)
    await staffPage.getByRole('button', { name: 'Send', exact: true }).click()
    await expect(staffPage.getByText(reply)).toBeVisible()
  })

  await test.step('the student reads the reply on their ticket', async () => {
    await studentPage.goto('/support')
    await studentPage.getByRole('button', { name: new RegExp(subject) }).click()
    await expect(studentPage.getByText(reply)).toBeVisible()
    await test.info().attach('student reads the reply', { body: await studentPage.screenshot(), contentType: 'image/png' })
    const mine = await call('GET', '/api/support/tickets', student.accessToken)
    const ticket = mine.items.find((t: { subject: string }) => t.subject === subject)
    expect(ticket.viaAssistant).toBe(true)
  })
})
