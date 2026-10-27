import { BOOTSTRAP_ADMIN_EMAIL, expect, test } from './fixtures.ts'

test('the Admin offboards a Member, burning their balance, then erases their personal data', async ({
  page,
  ownBackend: backend,
}) => {
  await backend.setClock('2026-03-01T09:00:00Z')
  await backend.seedMember([40])

  await page.goto(await backend.latestLink(BOOTSTRAP_ADMIN_EMAIL))
  await page.getByLabel('Your name').fill('Rosa Root')
  await page.getByRole('button', { name: 'Create my account' }).click()
  await expect(page).toHaveURL(/\/credits$/)

  await page.getByRole('link', { name: 'Members' }).first().click()
  const admin = page.getByRole('group', { name: 'Rosa Root' })
  await expect(admin).toContainText('Active')
  await expect(admin.getByRole('button')).toHaveCount(0)

  const member = page.getByRole('group', { name: 'Test Member' })
  await member.getByRole('button', { name: 'Offboard Test Member' }).click()
  const offboard = page.getByRole('alertdialog')
  await expect(offboard).toContainText('burned')
  await offboard.getByRole('button', { name: 'Cancel' }).click()
  await expect(offboard).toBeHidden()
  await expect(member).toContainText('Active')

  await member.getByRole('button', { name: 'Offboard Test Member' }).click()
  await page.getByRole('alertdialog').getByRole('button', { name: 'Offboard member' }).click()
  await expect(member.getByRole('status')).toContainText('10.00 credits burned')
  await expect(member).toContainText('Closed Mar 1, 2026')
  await expect(member.getByRole('button', { name: /Offboard/ })).toHaveCount(0)

  await member.getByRole('button', { name: /Erase the personal data of Test Member/ }).click()
  const erase = page.getByRole('alertdialog')
  await expect(erase).toContainText("can't be undone")
  await erase.getByRole('button', { name: 'Erase personal data' }).click()

  const erased = page.getByRole('group', { name: 'Former member' })
  await expect(erased).toContainText('Personal data erased')
  await expect(page.getByText('Test Member')).toHaveCount(0)
  await expect(erased.getByRole('button')).toHaveCount(0)
})
