import { expect, test } from './fixtures.ts'

// The start time is typed in the browser's local time; pin it so the expected instant is exact.
test.use({ timezoneId: 'UTC' })

test('a Member posts a Request at the sitter’s house with no area or address, and the cost goes into Escrow', async ({
  page,
  backend,
}) => {
  await backend.setClock('2026-03-01T09:00:00Z')
  const memberId = await backend.seedMember([40])
  await backend.signIn(page.context(), memberId)

  await page.goto('/requests/new')
  await page.getByLabel(/start time/i).fill('2026-03-02T10:00')
  await page.getByLabel(/duration/i).fill('120')
  await expect(page.getByLabel(/area/i)).toHaveCount(0)
  await expect(page.getByLabel(/address/i)).toHaveCount(0)
  await expect(page.getByText('2.00 credits')).toBeVisible()
  await page.getByRole('button', { name: /post request/i }).click()

  const row = page.getByRole('listitem').filter({ hasText: /sitter's house/i })
  await expect(row).toContainText('Posted')
  await expect(row).toContainText('2.00')
  await expect(row).toContainText('Mar 2, 2026, 10:00 AM UTC')

  await page.goto('/credits')
  await expect(page.getByRole('group', { name: 'Available' })).toContainText('8.00')
  await expect(page.getByRole('group', { name: 'In escrow' })).toContainText('2.00')
  await expect(
    page
      .getByRole('listitem')
      .filter({ hasText: /escrow/i })
      .filter({ hasText: '2.00' }),
  ).toHaveCount(1)
})

test('a Member sets their area and address once on their profile, and a Request at their house uses them', async ({
  page,
  backend,
}) => {
  await backend.setClock('2026-03-01T09:00:00Z')
  await backend.signIn(page.context(), await backend.seedMember([40]))

  await page.goto('/requests/new')
  await page.getByLabel('Location').selectOption('RequestingFamilysHouse')
  await expect(page.getByRole('alert')).toContainText(/add your address to your profile/i)
  await expect(page.getByRole('button', { name: /post request/i })).toBeDisabled()

  await page.goto('/profile')
  await page.getByLabel(/^area/i).fill('Riverside')
  await page.getByLabel(/exact address/i).fill('12 River Rd')
  await page.getByRole('button', { name: /save/i }).click()
  await expect(page.getByRole('status')).toContainText(/saved/i)

  await page.goto('/requests/new')
  await page.getByLabel(/start time/i).fill('2026-03-02T10:00')
  await page.getByLabel(/duration/i).fill('120')
  await page.getByLabel('Location').selectOption('RequestingFamilysHouse')
  await expect(page.getByText(/riverside, 12 river rd/i)).toBeVisible()
  await expect(page.getByText('2.50 credits')).toBeVisible()
  await page.getByRole('button', { name: /post request/i }).click()

  const row = page.getByRole('listitem').filter({ hasText: /riverside/i })
  await expect(row).toContainText('Posted')
  await expect(row).toContainText('2.50')
  await expect(row).toContainText('12 River Rd')
})

test('a Member cannot post inside the Minimum Lead Time', async ({ page, backend }) => {
  await backend.setClock('2026-03-01T09:00:00Z')
  await backend.signIn(page.context(), await backend.seedMember([40]))

  await page.goto('/requests/new')
  await page.getByLabel(/start time/i).fill('2026-03-01T12:00')
  await page.getByRole('button', { name: /post request/i }).click()

  await expect(page.getByRole('alert')).toContainText(/too soon/i)
})

test('a Member with no available credits cannot post', async ({ page, backend }) => {
  await backend.setClock('2026-03-01T09:00:00Z')
  await backend.signIn(page.context(), await backend.seedMember([0]))

  await page.goto('/requests/new')
  await page.getByLabel(/start time/i).fill('2026-03-02T10:00')

  await expect(page.getByRole('alert')).toContainText(/not enough credits/i)
  await expect(page.getByRole('button', { name: /post request/i })).toBeDisabled()
})
