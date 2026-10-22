import { BOOTSTRAP_ADMIN_EMAIL, expect, test } from './fixtures.ts'

test('an invited person creates an account with 10.0 credits, signs out and signs back in by email', async ({
  page,
  backend,
}) => {
  await backend.setClock('2026-03-01T09:00:00Z')

  // The bootstrap Admin Invitation is the way in; it was emailed at startup.
  await page.goto(await backend.latestLink(BOOTSTRAP_ADMIN_EMAIL))
  await expect(page.getByText(BOOTSTRAP_ADMIN_EMAIL)).toBeVisible()
  await page.getByLabel('Your name').fill('Rosa Root')
  await page.getByRole('button', { name: 'Create my account' }).click()

  await expect(page).toHaveURL(/\/credits$/)
  await expect(page.getByRole('group', { name: 'Available' })).toContainText('10.00')
  await expect(page.getByText('Rosa Root')).toBeVisible()

  await page.getByRole('button', { name: 'Sign out' }).click()
  await expect(page).toHaveURL(/\/sign-in$/)
  await page.goto('/credits')
  await expect(page).toHaveURL(/\/sign-in$/)

  await page.getByLabel('Email address').fill(BOOTSTRAP_ADMIN_EMAIL)
  await page.getByRole('button', { name: 'Email me a sign-in link' }).click()
  await expect(page.getByText(/If that address belongs to a Member/)).toBeVisible()

  const link = await backend.latestLink(BOOTSTRAP_ADMIN_EMAIL)
  await page.goto(link)
  await expect(page).toHaveURL(/\/credits$/)
  await expect(page.getByRole('group', { name: 'Available' })).toContainText('10.00')

  // The link works once.
  await page.getByRole('button', { name: 'Sign out' }).click()
  await page.goto(link)
  await expect(page.getByText(/can't be used/)).toBeVisible()
})
