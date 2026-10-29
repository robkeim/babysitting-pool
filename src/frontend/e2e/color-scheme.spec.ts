import { expect, test } from './fixtures.ts'

// Playwright emulates the browser's setting; the app starts on System, so it follows it.
test.use({ colorScheme: 'light' })

test('a Member picks a Colour Scheme, it overrides the system setting and outlives a reload', async ({
  page,
  backend,
}) => {
  await backend.setClock('2026-03-01T09:00:00Z')
  await backend.signIn(page.context(), await backend.seedMember([40]))
  const html = page.locator('html')

  await page.goto('/profile')
  await expect(page.locator('#color-scheme')).toHaveValue('system')
  await expect(html).not.toHaveClass(/dark/)

  // System follows the browser as it changes, with no reload.
  await page.emulateMedia({ colorScheme: 'dark' })
  await expect(html).toHaveClass(/dark/)
  await page.emulateMedia({ colorScheme: 'light' })
  await expect(html).not.toHaveClass(/dark/)

  await page.locator('#color-scheme').selectOption('dark')
  await expect(html).toHaveClass(/dark/)

  // Kept on the Member, so a reload shows it again and the browser setting no longer matters.
  await page.reload()
  await expect(page.locator('#color-scheme')).toHaveValue('dark')
  await expect(html).toHaveClass(/dark/)

  await page.locator('#color-scheme').selectOption('light')
  await page.emulateMedia({ colorScheme: 'dark' })
  await expect(html).not.toHaveClass(/dark/)
})
