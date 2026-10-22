import tailwindcss from '@tailwindcss/vite'
import { tanstackRouter } from '@tanstack/router-plugin/vite'
import react from '@vitejs/plugin-react'
import path from 'node:path'
import { defineConfig } from 'vitest/config'
import { configDefaults } from 'vitest/config'

// Playwright starts one API per worker and points this dev server at it (e2e/fixtures.ts).
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
      '/api': { target: E2E_API_URL ?? 'http://localhost:5120' },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    exclude: [...configDefaults.exclude, 'e2e/**'],
    setupFiles: ['./src/test/setup.ts'],
  },
})
