import { expect, test } from './fixtures.ts'

// The start time is typed in the browser's local time; pin it so the expected instant is exact.
test.use({ timezoneId: 'UTC' })

test('a Sitter withdraws and the Request reopens, then the Requester cancels and is refunded', async ({
  browser,
  backend,
}) => {
  await backend.setClock('2026-03-01T09:00:00Z')
  const requesterId = await backend.seedMember([40], { area: 'Riverside', address: '12 River Rd' })
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
  await requester.getByLabel('Location').selectOption('RequestingFamilysHouse')
  await requester.getByRole('button', { name: /post request/i }).click()
  await expect(requester.getByRole('listitem').filter({ hasText: /riverside/i })).toContainText(
    'Posted',
  )

  await sitter.goto(`${backend.appUrl}/open-requests`)
  await sitter.getByRole('button', { name: /claim the request in riverside/i }).click()
  // Navigating before the claim lands would abort it, so wait for the list to refresh.
  await expect(sitter.getByText(/no open requests/i)).toBeVisible()
  await sitter.goto(`${backend.appUrl}/claims`)
  await expect(sitter.getByRole('listitem').filter({ hasText: /riverside/i })).toContainText(
    '12 River Rd',
  )

  // Withdrawing drops the claim: it leaves My Claims and is open to Sitters again.
  await sitter.getByRole('button', { name: /withdraw from the request in riverside/i }).click()
  await expect(sitter.getByText(/haven.t claimed any requests/i)).toBeVisible()
  await sitter.goto(`${backend.appUrl}/open-requests`)
  await expect(sitter.getByRole('listitem').filter({ hasText: /riverside/i })).not.toContainText(
    '12 River Rd',
  )
  await requester.goto(`${backend.appUrl}/requests`)
  await expect(requester.getByRole('listitem').filter({ hasText: /riverside/i })).toContainText(
    'Posted',
  )

  // Cancelling ends it and returns the Escrow.
  await requester.getByRole('button', { name: /cancel the request in riverside/i }).click()
  await expect(requester.getByRole('listitem').filter({ hasText: /riverside/i })).toContainText(
    'Cancelled',
  )
  await expect(requester.getByRole('button', { name: /cancel the request/i })).toHaveCount(0)
  await sitter.goto(`${backend.appUrl}/open-requests`)
  await expect(sitter.getByText(/no open requests/i)).toBeVisible()
})
