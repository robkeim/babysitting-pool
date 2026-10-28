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
  test.setTimeout(600_000)
  await rm(OUTPUT, { recursive: true, force: true })

  await backend.setClock('2026-03-01T09:00:00Z')
  const requesterId = await backend.seedMember([80])
  const viewerId = await backend.seedMember([40, 1, 12])
  // Seeded before anything is posted, so they hold a Notification for each Request posted below; a Member of
  // their own keeps the panel's extra page loads clear of the viewer's rate limit.
  const readerId = await backend.seedMember([40])

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

  // The co-op's first Admin joins through the bootstrap Invitation, so the Admin screens can be shown.
  const joining = await browser.newContext({ timezoneId: 'UTC' })
  const joiningPage = await joining.newPage()
  await joiningPage.goto(invitation)
  await joiningPage.locator('#displayName').fill('Ada Admin')
  await joiningPage.getByRole('button').click()
  await expect(joiningPage.getByRole('heading', { level: 2 })).toBeVisible()
  const adminCookies = await joining.cookies()
  // The bell and its panel are behind the `notifications` flag (ADR 0019). Only the reader gets it: the bell polls,
  // and on every screen the viewer would exceed the per-minute rate limit at this speed.
  const flagResponse = await joining.request.put(`${backend.appUrl}/api/admin/feature-flags`, {
    headers: { 'x-requested-with': 'fetch' },
    data: {
      name: 'notifications',
      enabled: true,
      rolloutPercent: 0,
      allowedMemberIds: [readerId],
      deniedMemberIds: [],
      roleRollouts: [],
    },
  })
  expect(flagResponse.ok()).toBe(true)
  await joining.close()

  for (const language of LANGUAGES) {
    for (const who of ['member', 'reader', 'admin']) {
      const setup = await browser.newContext({ timezoneId: 'UTC' })
      if (who === 'admin') await setup.addCookies(adminCookies)
      else await backend.signIn(setup, who === 'reader' ? readerId : viewerId)
      const setupPage = await setup.newPage()
      await setupPage.goto(`${backend.appUrl}/credits`)
      await setupPage
        .getByRole('combobox', { name: 'Language' })
        .selectOption({ label: language.label })
      await expect(setupPage.getByRole('heading', { level: 1 })).toHaveText(language.heading)
      await setup.close()
    }

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

        const readerContext = await browser.newContext({
          colorScheme: scheme,
          viewport,
          timezoneId: 'UTC',
        })
        await backend.signIn(readerContext, readerId)
        const readerPage = await readerContext.newPage()
        await readerPage.goto(`${backend.appUrl}/credits`)
        await readerPage.locator('[data-slot="popover-trigger"]').click()
        const panel = readerPage.getByRole('dialog')
        await expect(panel.getByRole('listitem').first()).toBeVisible()
        await expect(panel).toHaveCSS('opacity', '1')
        await capture(
          readerPage,
          'notifications-panel',
          `${language.tag}-${scheme}-${viewport.name}`,
        )
        await readerContext.close()

        const adminContext = await browser.newContext({
          colorScheme: scheme,
          viewport,
          timezoneId: 'UTC',
        })
        await adminContext.addCookies(adminCookies)
        const adminPage = await adminContext.newPage()
        await adminPage.goto(`${backend.appUrl}/admin/feature-flags`)
        await expect(adminPage.getByRole('heading', { level: 2 })).toBeVisible()
        await capture(adminPage, 'feature-flags', `${language.tag}-${scheme}-${viewport.name}`)
        // The rules are the same for every Member; the Admin's context spares the viewer's rate limit.
        await adminPage.goto(`${backend.appUrl}/how-it-works`)
        await expect(adminPage.locator('#demurrage')).toBeVisible()
        await capture(adminPage, 'how-it-works', `${language.tag}-${scheme}-${viewport.name}`)
        await adminContext.close()
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
  // The area and address come from the profile, so set the profile before each Request.
  await page.goto(`${appUrl}/profile`)
  await page.getByLabel(/^area/i).fill(request.area)
  await page.getByLabel(/exact address/i).fill('12 River Rd')
  await page.getByRole('button', { name: /save/i }).click()
  await expect(page.getByRole('status')).toContainText(/saved/i)

  await page.goto(`${appUrl}/requests/new`)
  await page.getByLabel('Location').selectOption('RequestingFamilysHouse')
  await page.getByLabel(/start time/i).fill(request.start)
  await page.getByLabel(/duration/i).fill(request.minutes)
  await page.getByRole('button', { name: /post request/i }).click()
  await expect(
    page.getByRole('listitem').filter({ hasText: new RegExp(request.area, 'i') }),
  ).toBeVisible()
}
