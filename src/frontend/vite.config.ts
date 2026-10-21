/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite'
import { tanstackRouter } from '@tanstack/router-plugin/vite'
import react from '@vitejs/plugin-react'
import path from 'node:path'
import { defineConfig } from 'vite'

// The API seeds this Member in Development; there is no real sign-in yet (see DevIdentity in the API).
const DEV_MEMBER_ID = '00000000-0000-0000-0000-00000000de01'

// https://vite.dev/config/
export default defineConfig({
  plugins: [tanstackRouter({ target: 'react', autoCodeSplitting: true }), react(), tailwindcss()],
  resolve: {
    alias: { '@': path.resolve(import.meta.dirname, './src') },
  },
  server: {
    // One origin locally, as in production (ADR 0006): the SPA proxies /api to the API.
    proxy: {
      '/api': { target: 'http://localhost:5120', headers: { 'X-Dev-Member-Id': DEV_MEMBER_ID } },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
  },
})
