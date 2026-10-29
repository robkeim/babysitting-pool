import { type Browser, type Page } from '@playwright/test'
import { expect, test, type Backend } from './fixtures.ts'

// The start time is typed in the browser's local time; pin it so the expected instant is exact.
test.use({ timezoneId: 'UTC' })

async function signedInPage(browser: Browser, backend: Backend, memberId: string): Promise<Page> {
  const context = await browser.newContext({ timezoneId: 'UTC' })
  await backend.signIn(context, memberId)
  return context.newPage()
}

async function postRequest(page: Page, appUrl: string) {
  await page.goto(`${appUrl}/requests/new`)
  await page.getByLabel(/start time/i).fill('2026-03-02T10:00')
  await page.getByLabel(/duration/i).fill('120')
  await page.getByLabel('Location').selectOption('RequestingFamilysHouse')
  await page.getByRole('button', { name: /post request/i }).click()
  await expect(page.getByRole('listitem').filter({ hasText: /riverside/i })).toContainText('Posted')
}

test('a Member sees a posted Request arrive live, and the Requester sees it claimed', async ({
  browser,
  backend,
}) => {
  await backend.setClock('2026-03-01T09:00:00Z')
  const requesterId = await backend.seedMember([40], { area: 'Riverside', address: '12 River Rd' })
  const sitterId = await backend.seedMember([40])
  const requester = await signedInPage(browser, backend, requesterId)
  const sitter = await signedInPage(browser, backend, sitterId)

  // Both are already on a page, so nothing below reloads.
  await requester.goto(`${backend.appUrl}/requests`)
  await sitter.goto(`${backend.appUrl}/open-requests`)
  await expect(sitter.getByRole('button', { name: 'Notifications' })).toBeVisible()
  await expect(requester.getByRole('button', { name: 'Notifications' })).toBeVisible()

  const other = await signedInPage(browser, backend, requesterId)
  await postRequest(other, backend.appUrl)

  await expect(sitter.getByRole('status')).toContainText('New request in Riverside')
  await expect(sitter.getByRole('button', { name: 'Notifications, 1 waiting' })).toBeVisible()
  // The Requester is not told about their own Request.
  await expect(requester.getByRole('button', { name: 'Notifications' })).toBeVisible()

  await sitter.getByRole('button', { name: /claim the request in riverside/i }).click()

  await expect(requester.getByRole('status')).toContainText('claimed your request')
  await expect(requester.getByRole('button', { name: 'Notifications, 1 waiting' })).toBeVisible()
})

test('dismissing a Notification in one tab clears it in the other', async ({
  browser,
  backend,
}) => {
  await backend.setClock('2026-03-01T09:00:00Z')
  const requesterId = await backend.seedMember([40], { area: 'Riverside', address: '12 River Rd' })
  const sitterId = await backend.seedMember([40])
  const requester = await signedInPage(browser, backend, requesterId)
  await postRequest(requester, backend.appUrl)

  const sitter = await signedInPage(browser, backend, sitterId)
  const firstTab = sitter
  const secondTab = await sitter.context().newPage()
  await firstTab.goto(`${backend.appUrl}/credits`)
  await secondTab.goto(`${backend.appUrl}/credits`)
  await expect(firstTab.getByRole('button', { name: 'Notifications, 1 waiting' })).toBeVisible()
  await expect(secondTab.getByRole('button', { name: 'Notifications, 1 waiting' })).toBeVisible()

  await firstTab.getByRole('button', { name: /^Notifications/ }).click()
  await firstTab.getByRole('button', { name: 'Dismiss all' }).click()

  await expect(firstTab.getByText("You're all caught up.")).toBeVisible()
  await expect(secondTab.getByRole('button', { name: 'Notifications' })).toBeVisible()
})
