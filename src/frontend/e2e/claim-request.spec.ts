import { expect, test } from './fixtures.ts'

// The start time is typed in the browser's local time; pin it so the expected instant is exact.
test.use({ timezoneId: 'UTC' })

test('a Sitter claims an open Request, and the loser of a race is told it is gone', async ({
  browser,
  backend,
}) => {
  await backend.setClock('2026-03-01T09:00:00Z')
  const requesterId = await backend.seedMember([40])
  const winnerId = await backend.seedMember([40])
  const loserId = await backend.seedMember([40])
  const [requester, winner, loser] = await Promise.all(
    [requesterId, winnerId, loserId].map(async (memberId) => {
      const context = await browser.newContext({ timezoneId: 'UTC' })
      await backend.signIn(context, memberId)
      return context.newPage()
    }),
  )
  if (!requester || !winner || !loser) throw new Error('contexts were not created')

  await requester.goto(`${backend.appUrl}/requests/new`)
  await requester.getByLabel(/start time/i).fill('2026-03-02T10:00')
  await requester.getByLabel(/duration/i).fill('120')
  await requester.getByLabel(/^area/i).fill('Riverside')
  await requester.getByLabel(/exact address/i).fill('12 River Rd')
  await requester.getByRole('button', { name: /post request/i }).click()
  await expect(requester.getByRole('row', { name: /riverside/i })).toContainText('Posted')

  // Both Sitters see it with the rough area only; the requester does not see their own.
  await winner.goto(`${backend.appUrl}/open-requests`)
  await loser.goto(`${backend.appUrl}/open-requests`)
  await expect(winner.getByRole('listitem').filter({ hasText: /riverside/i })).not.toContainText(
    '12 River Rd',
  )
  await expect(loser.getByRole('listitem').filter({ hasText: /riverside/i })).toBeVisible()
  await requester.goto(`${backend.appUrl}/open-requests`)
  await expect(requester.getByText(/no open requests/i)).toBeVisible()

  // The first claim wins.
  await winner.getByRole('button', { name: /claim the request in riverside/i }).click()
  await expect(winner.getByText(/no open requests/i)).toBeVisible()
  await winner.goto(`${backend.appUrl}/claims`)
  const claim = winner.getByRole('row', { name: /riverside/i })
  await expect(claim).toContainText('Claimed')
  await expect(claim).toContainText('12 River Rd')

  // The other Sitter's list was loaded before that claim, so theirs is rejected and the list refreshes.
  await loser.getByRole('button', { name: /claim the request in riverside/i }).click()
  await expect(loser.getByRole('alert')).toContainText(/no longer available/i)
  await expect(loser.getByText(/no open requests/i)).toBeVisible()

  // The Requester sees it Claimed; nothing moved in the Ledger until the start time.
  await requester.goto(`${backend.appUrl}/requests`)
  await expect(requester.getByRole('row', { name: /riverside/i })).toContainText('Claimed')
})

test('a Request past its Claim Cutoff is no longer open', async ({ browser, page, backend }) => {
  await backend.setClock('2026-03-01T09:00:00Z')
  const requesterId = await backend.seedMember([40])
  const sitterId = await backend.seedMember([40])
  const requester = await browser.newContext({ timezoneId: 'UTC' })
  await backend.signIn(requester, requesterId)
  const requesterPage = await requester.newPage()
  await requesterPage.goto(`${backend.appUrl}/requests/new`)
  await requesterPage.getByLabel(/start time/i).fill('2026-03-01T20:00')
  await requesterPage.getByLabel(/^area/i).fill('Riverside')
  await requesterPage.getByLabel(/exact address/i).fill('12 River Rd')
  await requesterPage.getByRole('button', { name: /post request/i }).click()
  await expect(requesterPage.getByRole('row', { name: /riverside/i })).toBeVisible()
  await backend.signIn(page.context(), sitterId)
  await page.goto('/open-requests')
  await expect(page.getByRole('listitem').filter({ hasText: /riverside/i })).toBeVisible()

  // Claim Cutoff is 3 hours before the 20:00 start.
  await backend.setClock('2026-03-01T17:00:00Z')
  await page.reload()

  await expect(page.getByText(/no open requests/i)).toBeVisible()
})
