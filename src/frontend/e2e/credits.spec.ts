import { expect, test } from './fixtures.ts'

test('a Member sees their balance and Ledger as credits', async ({ page, backend }) => {
  await backend.setClock('2026-03-01T09:00:00Z')
  // 10.00 then 0.25 credits as quarter-credits: the 41 on the account is the round trip of 10.25.
  const memberId = await backend.seedMember([40, 1])
  await backend.signIn(page.context(), memberId)

  await page.goto('/credits')

  await expect(page.getByRole('group', { name: 'Available' })).toContainText('10.25')
  await expect(page.getByRole('group', { name: 'In escrow' })).toContainText('0.00')
  const rows = page.getByRole('row', { name: /grant/i })
  await expect(rows).toHaveCount(2)
  await expect(rows.filter({ hasText: '10.00' })).toContainText('Mar 1, 2026, 9:00 AM UTC')
  await expect(rows.filter({ hasText: '0.25' })).toContainText('Mar 1, 2026, 10:00 AM UTC')
})
