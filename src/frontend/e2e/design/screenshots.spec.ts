import { mkdir, rm } from 'node:fs/promises'
import path from 'node:path'
import { type Page } from '@playwright/test'
import { expect, test } from '../fixtures.ts'
import { FRONTEND_ROOT } from '../paths.ts'

// The design check for agents (docs/design.md): every key screen in each Language, colour scheme and width,
// saved as PNGs to look at. Run with `npm run design:screenshots`; it is not part of the gate.
const OUTPUT = path.join(FRONTEND_ROOT, 'design-screenshots')

const LANGUAGES = [
  { tag: 'en-US', label: 'English (US)', heading: /^Babysitting Pool$/ },
  // The padded, accented test Language stands in for every real translation (ADR 0009).
  { tag: 'en-XA', label: 'Ünïcödé Tëst', heading: /^⏩/u },
]
const SCHEMES = ['light', 'dark'] as const
const VIEWPORTS = [
  { name: 'mobile', width: 375, height: 812 },
  { name: 'desktop', width: 1280, height: 800 },
]
const SCREENS = [
  { name: 'credits', path: '/credits' },
  { name: 'open-requests', path: '/open-requests' },
]

test.use({ timezoneId: 'UTC' })

test('screenshots of the key screens', async ({ browser, backend }) => {
  test.setTimeout(300_000)
  await rm(OUTPUT, { recursive: true, force: true })

  await backend.setClock('2026-03-01T09:00:00Z')
  const requesterId = await backend.seedMember([80])
  const viewerId = await backend.seedMember([40, 1, 12])

  const requester = await browser.newContext({ timezoneId: 'UTC' })
  await backend.signIn(requester, requesterId)
  const requesterPage = await requester.newPage()
  const requests = [
    { start: '2026-03-02T10:00', minutes: '120', area: 'Riverside' },
    { start: '2026-03-03T18:30', minutes: '240', area: 'Hilltop Meadows' },
    { start: '2026-03-04T08:15', minutes: '90', area: 'Old Mill District' },
  ]
  for (const request of requests) {
    await requesterPage.goto(`${backend.appUrl}/requests/new`)
    await requesterPage.getByLabel(/start time/i).fill(request.start)
    await requesterPage.getByLabel(/duration/i).fill(request.minutes)
    await requesterPage.getByLabel(/^area/i).fill(request.area)
    await requesterPage.getByLabel(/exact address/i).fill('12 River Rd')
    await requesterPage.getByRole('button', { name: /post request/i }).click()
    await expect(
      requesterPage.getByRole('row', { name: new RegExp(request.area, 'i') }),
    ).toBeVisible()
  }
  await requester.close()

  // Signed out, so only in the default Language (the Language is kept on the Member).
  for (const scheme of SCHEMES) {
    for (const viewport of VIEWPORTS) {
      const context = await browser.newContext({
        colorScheme: scheme,
        viewport,
        timezoneId: 'UTC',
      })
      const page = await context.newPage()
      await page.goto(`${backend.appUrl}/sign-in`)
      await expect(page.getByLabel(/email address/i)).toBeVisible()
      await capture(page, 'sign-in', `en-US-${scheme}-${viewport.name}`)
      await context.close()
    }
  }

  for (const language of LANGUAGES) {
    const setup = await browser.newContext({ timezoneId: 'UTC' })
    await backend.signIn(setup, viewerId)
    const setupPage = await setup.newPage()
    await setupPage.goto(`${backend.appUrl}/credits`)
    await setupPage
      .getByRole('combobox', { name: 'Language' })
      .selectOption({ label: language.label })
    await expect(setupPage.getByRole('heading', { level: 1 })).toHaveText(language.heading)
    await setup.close()

    for (const scheme of SCHEMES) {
      for (const viewport of VIEWPORTS) {
        const context = await browser.newContext({
          colorScheme: scheme,
          viewport,
          timezoneId: 'UTC',
        })
        await backend.signIn(context, viewerId)
        const page = await context.newPage()
        for (const screen of SCREENS) {
          await page.goto(`${backend.appUrl}${screen.path}`)
          await expect(page.getByRole('heading', { level: 2 })).toBeVisible()
          await expect(page.getByRole('listitem').first()).toBeVisible()
          await capture(page, screen.name, `${language.tag}-${scheme}-${viewport.name}`)
        }
        await context.close()
      }
    }
  }
})

async function capture(page: Page, screen: string, variant: string) {
  // Grow the viewport to the whole page so fixed bars sit where a reader would see them, then check nothing
  // forces a sideways scroll, the commonest way a long translation breaks a layout.
  const { width } = page.viewportSize() ?? { width: 1280 }
  const height = await page.evaluate<number>('document.documentElement.scrollHeight')
  await page.setViewportSize({ width, height: Math.max(height, 400) })
  const overflow = await page.evaluate<number>(
    'document.documentElement.scrollWidth - document.documentElement.clientWidth',
  )
  expect
    .soft(overflow, `${screen} ${variant} scrolls sideways by ${overflow}px`)
    .toBeLessThanOrEqual(0)
  // Content spilling out of its own box overlaps its neighbours without scrolling the page.
  const spilling = await page.evaluate<string[]>(`[...document.querySelectorAll('body *')]
    .filter((element) => !['SELECT', 'svg', 'path'].includes(element.tagName) && element.clientWidth > 0)
    .filter((element) => !String(element.className).includes('sr-only'))
    .filter((element) => element.scrollWidth > element.clientWidth + 1)
    .map((element) => element.tagName.toLowerCase() + '.' + String(element.className).slice(0, 60))`)
  expect.soft(spilling, `${screen} ${variant} has content spilling out of its box`).toEqual([])
  await mkdir(path.join(OUTPUT, screen), { recursive: true })
  await page.screenshot({ path: path.join(OUTPUT, screen, `${variant}.png`) })
  await page.setViewportSize({ width, height })
}
