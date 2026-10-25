import { defineConfig } from '@playwright/test'

// Screenshots for the design check (docs/design.md): same real stack as the E2E suite, but outside the gate.
export default defineConfig({
  testDir: './e2e/design',
  globalSetup: './e2e/global-setup.ts',
  workers: 1,
  reporter: 'list',
  outputDir: './test-results/design',
  use: { browserName: 'chromium' },
})
