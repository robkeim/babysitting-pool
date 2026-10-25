import { mkdir, rm } from 'node:fs/promises'
import path from 'node:path'
import { type Page } from '@playwright/test'
import { BOOTSTRAP_ADMIN_EMAIL, expect, test } from '../fixtures.ts'
import { FRONTEND_ROOT } from '../paths.ts'

// The design check for agents (docs/design.md): every key screen in each Language, colour scheme and width,
// saved as PNGs to look at. Run with `npm run design:screenshots`; it is not part of the gate.
const OUTPUT = path.join(FRONTEND_ROOT, 'design-screenshots')

const LANGUAGES = [
  { tag: 'en-US', label: '🇺🇸 English', heading: /^Babysitting Pool$/ },
  // The padded, accented test Language stands in for every real translation (ADR 0009).
  { tag: 'en-XA', label: '🏴‍☠️ Ünïcödé Tëst', heading: /^⏩/u },
]
const SCHEMES = ['light', 'dark'] as const
const VIEWPORTS = [
  { name: 'mobile', width: 375, height: 812 },
  { name: 'desktop', width: 1280, height: 800 },
]
const SCREENS = [
  { name: 'credits', path: '/credits' },
  { name: 'open-requests', path: '/open-requests' },
  { name: 'my-requests', path: '/requests' },
  { name: 'my-claims', path: '/claims' },
  { name: 'post-request', path: '/requests/new' },
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
  for (const request of [
    { start: '2026-03-02T10:00', minutes: '120', area: 'Riverside' },
    { start: '2026-03-03T18:30', minutes: '240', area: 'Hilltop Meadows' },
    { start: '2026-03-04T08:15', minutes: '90', area: 'Old Mill District' },
  ]) {
    await postRequest(requesterPage, backend.appUrl, request)
  }
  await requester.close()

  // The viewer has posted one Request and cancelled another, and claimed one, so every list has some states.
  const setupViewer = await browser.newContext({ timezoneId: 'UTC' })
  await backend.signIn(setupViewer, viewerId)
  const viewerPage = await setupViewer.newPage()
  await postRequest(viewerPage, backend.appUrl, {
    start: '2026-03-05T09:00',
    minutes: '180',
    area: 'Maple Court',
  })
  await postRequest(viewerPage, backend.appUrl, {
    start: '2026-03-06T17:00',
    minutes: '60',
    area: 'Cedar Lane',
  })
  await viewerPage.getByRole('button', { name: /cancel the request in cedar lane/i }).click()
  await expect(
    viewerPage.getByRole('button', { name: /cancel the request in cedar lane/i }),
  ).toHaveCount(0)
  await viewerPage.goto(`${backend.appUrl}/open-requests`)
  await viewerPage.getByRole('button', { name: /claim the request in riverside/i }).click()
  await expect(
    viewerPage.getByRole('button', { name: /claim the request in riverside/i }),
  ).toHaveCount(0)
  await setupViewer.close()

  // Signed out, so only in the default Language (the Language is kept on the Member).
  const invitation = await backend.latestLink(BOOTSTRAP_ADMIN_EMAIL)
  const SIGNED_OUT = [
    { name: 'sign-in', url: `${backend.appUrl}/sign-in`, ready: '#email' },
    { name: 'invitation', url: invitation, ready: '#displayName' },
    { name: 'invitation-unusable', url: `${backend.appUrl}/invitation?token=nope`, ready: null },
    { name: 'sign-in-link-unusable', url: `${backend.appUrl}/sign-in/verify`, ready: null },
  ]
  for (const screen of SIGNED_OUT) {
    for (const scheme of SCHEMES) {
      for (const viewport of VIEWPORTS) {
        const context = await browser.newContext({
          colorScheme: scheme,
          viewport,
          timezoneId: 'UTC',
        })
        const page = await context.newPage()
        await page.goto(screen.url)
        if (screen.ready) await expect(page.locator(screen.ready)).toBeVisible()
        else await expect(page.getByRole('alert')).toBeVisible()
        await capture(page, screen.name, `en-US-${scheme}-${viewport.name}`)
        await context.close()
      }
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
          await expect(
            screen.name === 'post-request'
              ? page.locator('#startsAt')
              : page.getByRole('listitem').first(),
          ).toBeVisible()
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

async function postRequest(
  page: Page,
  appUrl: string,
  request: { start: string; minutes: string; area: string },
) {
  await page.goto(`${appUrl}/requests/new`)
  await page.getByLabel(/start time/i).fill(request.start)
  await page.getByLabel(/duration/i).fill(request.minutes)
  await page.getByLabel(/^area/i).fill(request.area)
  await page.getByLabel(/exact address/i).fill('12 River Rd')
  await page.getByRole('button', { name: /post request/i }).click()
  await expect(
    page.getByRole('listitem').filter({ hasText: new RegExp(request.area, 'i') }),
  ).toBeVisible()
}
