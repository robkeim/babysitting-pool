import { expect, test } from './fixtures.ts'

test('a Member switches Language, the visible text changes and the choice outlives a reload', async ({
  page,
  backend,
}) => {
  await backend.setClock('2026-03-01T09:00:00Z')
  await backend.signIn(page.context(), await backend.seedMember([40]))

  await page.goto('/credits')
  await expect(page.getByRole('heading', { level: 1, name: 'Babysitting Pool' })).toBeVisible()

  await page.getByRole('combobox', { name: 'Language' }).selectOption({ label: 'Ünïcödé Tëst' })
  await expect(page.getByRole('heading', { level: 1, name: /^⏩/u })).toBeVisible()
  await expect(page.getByRole('group', { name: /⏩/u }).first()).toBeVisible()

  // The Language is kept on the Member, not in the browser tab.
  await page.reload()
  await expect(page.getByRole('heading', { level: 1, name: /^⏩/u })).toBeVisible()

  await page.getByRole('combobox').selectOption({ label: 'English (US)' })
  await expect(page.getByRole('heading', { level: 1, name: 'Babysitting Pool' })).toBeVisible()
  await expect(page.getByRole('group', { name: 'Available' })).toContainText('10.00')
})
