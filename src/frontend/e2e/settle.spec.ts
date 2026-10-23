import { expect, test } from './fixtures.ts'

// The start time is typed in the browser's local time; pin it so the expected instant is exact.
test.use({ timezoneId: 'UTC' })

test('Settle pays a Claimed Request out to the Sitter when its start time arrives', async ({
  browser,
  backend,
}) => {
  await backend.setClock('2026-03-01T09:00:00Z')
  const requesterId = await backend.seedMember([40])
  const sitterId = await backend.seedMember([40])
  const [requester, sitter] = await Promise.all(
    [requesterId, sitterId].map(async (memberId) => {
      const context = await browser.newContext({ timezoneId: 'UTC' })
      await backend.signIn(context, memberId)
      return context.newPage()
    }),
  )
  if (!requester || !sitter) throw new Error('contexts were not created')

  await requester.goto(`${backend.appUrl}/requests/new`)
  await requester.getByLabel(/start time/i).fill('2026-03-02T10:00')
  await requester.getByLabel(/duration/i).fill('120')
  await requester.getByLabel(/^area/i).fill('Payout Park')
  await requester.getByLabel(/exact address/i).fill('12 River Rd')
  await requester.getByRole('button', { name: /post request/i }).click()
  await expect(requester.getByRole('row', { name: /payout park/i })).toContainText('Posted')

  await sitter.goto(`${backend.appUrl}/open-requests`)
  await sitter.getByRole('button', { name: /claim the request in payout park/i }).click()
  // Other tests' open Requests share this backend, so check the one row rather than an empty list.
  await expect(sitter.getByRole('row', { name: /payout park/i })).toHaveCount(0)

  // The Escrow stays put until the start time, then Settle releases it to the Sitter.
  await requester.goto(`${backend.appUrl}/credits`)
  await expect(requester.getByRole('group', { name: 'In escrow' })).toContainText('2.00')
  await backend.setClock('2026-03-02T10:00:00Z')
  await backend.settle()

  await requester.goto(`${backend.appUrl}/credits`)
  await expect(requester.getByRole('group', { name: 'Available' })).toContainText('8.00')
  await expect(requester.getByRole('group', { name: 'In escrow' })).toContainText('0.00')
  await sitter.goto(`${backend.appUrl}/credits`)
  await expect(sitter.getByRole('group', { name: 'Available' })).toContainText('12.00')
  await expect(sitter.getByRole('row', { name: /payout/i })).toContainText('2.00')

  await requester.goto(`${backend.appUrl}/requests`)
  await expect(requester.getByRole('row', { name: /payout park/i })).toContainText('Completed')
})

test('Settle refunds a Request nobody claimed once its Claim Cutoff passes', async ({
  page,
  backend,
}) => {
  await backend.setClock('2026-03-01T09:00:00Z')
  await backend.signIn(page.context(), await backend.seedMember([40]))

  await page.goto('/requests/new')
  await page.getByLabel(/start time/i).fill('2026-03-02T10:00')
  await page.getByLabel(/duration/i).fill('120')
  await page.getByLabel(/^area/i).fill('Expiry Heights')
  await page.getByLabel(/exact address/i).fill('12 River Rd')
  await page.getByRole('button', { name: /post request/i }).click()
  await expect(page.getByRole('row', { name: /expiry heights/i })).toContainText('Posted')

  // Claim Cutoff is 3 hours before the 10:00 start.
  await backend.setClock('2026-03-02T07:00:00Z')
  await backend.settle()

  await page.goto('/credits')
  await expect(page.getByRole('group', { name: 'Available' })).toContainText('10.00')
  await expect(page.getByRole('group', { name: 'In escrow' })).toContainText('0.00')
  await expect(page.getByRole('row', { name: /refund/i })).toContainText('2.00')
  await page.goto('/requests')
  await expect(page.getByRole('row', { name: /expiry heights/i })).toContainText('Expired')
})
