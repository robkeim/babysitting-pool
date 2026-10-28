import { BOOTSTRAP_ADMIN_EMAIL, expect, test } from './fixtures.ts'

test('the Admin invites a person, re-sends the invitation and the person joins with 10.0 credits', async ({
  browser,
  page,
  ownBackend: backend,
}) => {
  await backend.setClock('2026-03-01T09:00:00Z')
  const email = `invitee-${crypto.randomUUID()}@example.test`

  // The bootstrap Admin Invitation is the way in; this journey has its own stack so it can consume it.
  await page.goto(await backend.latestLink(BOOTSTRAP_ADMIN_EMAIL))
  await page.getByLabel('Your name').fill('Rosa Root')
  await page.getByRole('button', { name: 'Create my account' }).click()
  await expect(page).toHaveURL(/\/credits$/)

  await page.getByRole('button', { name: 'Admin' }).click()
  await page.getByRole('menuitem', { name: 'Invitations' }).click()
  await page.getByLabel('Email address').fill(email)
  await page.getByRole('button', { name: 'Send invitation' }).click()
  await expect(page.getByRole('status')).toContainText(`Invitation sent to ${email}.`)
  const invitation = page.getByRole('group', { name: email })
  await expect(invitation).toContainText('Pending')
  const firstLink = await backend.latestLink(email)

  // A re-send replaces the link, so the first one no longer works.
  await invitation.getByRole('button', { name: /Re-send/ }).click()
  await expect(invitation.getByRole('status')).toHaveText('A new link was sent.')
  const secondLink = await backend.latestLink(email)
  expect(secondLink).not.toBe(firstLink)

  const invitee = await browser.newContext()
  const inviteePage = await invitee.newPage()
  await inviteePage.goto(firstLink)
  await expect(inviteePage.getByText(/can't be used/)).toBeVisible()
  await inviteePage.goto(secondLink)
  await inviteePage.getByLabel('Your name').fill('Ivy Invitee')
  await inviteePage.getByRole('button', { name: 'Create my account' }).click()
  await expect(inviteePage.getByRole('group', { name: 'Available' })).toContainText('10.00')
  await invitee.close()

  await page.reload()
  await expect(page.getByRole('group', { name: email })).toContainText('Accepted')
  await expect(page.getByRole('group', { name: email }).getByRole('button')).toHaveCount(0)
})
