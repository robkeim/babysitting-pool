/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite'
import { tanstackRouter } from '@tanstack/router-plugin/vite'
import react from '@vitejs/plugin-react'
import path from 'node:path'
import { defineConfig } from 'vite'
import { configDefaults } from 'vitest/config'

// The API seeds this Member in Development; there is no real sign-in yet (see DevIdentity in the API).
const DEV_MEMBER_ID = '00000000-0000-0000-0000-00000000de01'

// Playwright starts one API per worker and points this dev server at it (e2e/fixtures.ts). It names the Member
// per test itself, so no identity header is injected then.
const E2E_API_URL = process.env.E2E_API_URL

// https://vite.dev/config/
export default defineConfig({
  plugins: [tanstackRouter({ target: 'react', autoCodeSplitting: true }), react(), tailwindcss()],
  resolve: {
    alias: { '@': path.resolve(import.meta.dirname, './src') },
  },
  server: {
    // One origin locally, as in production (ADR 0006): the SPA proxies /api to the API.
    proxy: {
      '/api': E2E_API_URL
        ? { target: E2E_API_URL }
        : { target: 'http://localhost:5120', headers: { 'X-Dev-Member-Id': DEV_MEMBER_ID } },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    exclude: [...configDefaults.exclude, 'e2e/**'],
    setupFiles: ['./src/test/setup.ts'],
  },
})
