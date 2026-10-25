import { defineConfig } from '@playwright/test'

// The real API and SPA, no API mocks (docs/testing.md). Each worker starts its own backend and dev server in
// e2e/fixtures.ts, so there is no `webServer` here.
export default defineConfig({
  testDir: './e2e',
  testIgnore: 'design/**',
  globalSetup: './e2e/global-setup.ts',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  reporter: process.env.CI ? 'github' : 'list',
  outputDir: './test-results',
  use: {
    browserName: 'chromium',
    trace: 'retain-on-failure',
  },
})
