import { test as base, type Page } from '@playwright/test'
import { spawn, type ChildProcess } from 'node:child_process'
import { mkdtemp, rm } from 'node:fs/promises'
import { createServer } from 'node:net'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { API_DLL, API_PROJECT, FRONTEND_ROOT } from './paths.ts'

const IDENTITY_HEADER = 'X-Dev-Member-Id'

/** One worker's private stack: its own API process, SQLite file, fake clock and SPA dev server. */
export interface Backend {
  /** The SPA origin; it proxies `/api` to this worker's API, so the browser sees one origin (ADR 0006). */
  appUrl: string
  /** Seeds a Member whose Credit Account holds the given Grants (quarter-credits, ADR 0001). */
  seedMember(grantsQuarterCredits: number[]): Promise<string>
  /** Sets the server clock to an absolute instant. */
  setClock(now: string): Promise<void>
}

async function freePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = createServer()
    server.once('error', reject)
    server.listen(0, '127.0.0.1', () => {
      const { port } = server.address() as { port: number }
      server.close(() => {
        resolve(port)
      })
    })
  })
}

async function waitFor(url: string, process: ChildProcess, name: string): Promise<void> {
  const deadline = Date.now() + 60_000
  while (Date.now() < deadline) {
    if (process.exitCode !== null) throw new Error(`${name} exited with code ${process.exitCode}`)
    try {
      if ((await fetch(url)).ok) return
    } catch {
      // not listening yet
    }
    await new Promise((resolve) => setTimeout(resolve, 200))
  }
  throw new Error(`${name} did not become ready at ${url}`)
}

async function stop(child: ChildProcess): Promise<void> {
  if (child.exitCode !== null) return
  const exited = new Promise((resolve) => child.once('exit', resolve))
  child.kill('SIGTERM')
  await exited
}

async function post(url: string, body: unknown, method = 'POST'): Promise<Response> {
  const response = await fetch(url, {
    method,
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok)
    throw new Error(`${method} ${url} failed: ${response.status} ${await response.text()}`)
  return response
}

export const test = base.extend<object, { backend: Backend }>({
  backend: [
    // eslint-disable-next-line no-empty-pattern -- Playwright requires a destructuring pattern here
    async ({}, provide) => {
      const dataDirectory = await mkdtemp(path.join(tmpdir(), 'babysitting-e2e-'))
      const apiPort = await freePort()
      const appPort = await freePort()
      const apiUrl = `http://127.0.0.1:${apiPort}`
      const appUrl = `http://127.0.0.1:${appPort}`

      const api = spawn('dotnet', [API_DLL], {
        cwd: API_PROJECT,
        stdio: ['ignore', 'ignore', 'inherit'],
        env: {
          ...process.env,
          ASPNETCORE_ENVIRONMENT: 'E2E',
          ASPNETCORE_URLS: apiUrl,
          ConnectionStrings__Default: `Data Source=${path.join(dataDirectory, 'babysitting.db')}`,
        },
      })
      const spa = spawn(
        path.join(FRONTEND_ROOT, 'node_modules/.bin/vite'),
        ['--host', '127.0.0.1', '--port', String(appPort), '--strictPort'],
        { cwd: FRONTEND_ROOT, stdio: 'inherit', env: { ...process.env, E2E_API_URL: apiUrl } },
      )

      try {
        await Promise.all([waitFor(`${apiUrl}/health`, api, 'API'), waitFor(appUrl, spa, 'SPA')])
        await provide({
          appUrl,
          async seedMember(grantsQuarterCredits) {
            const response = await post(`${apiUrl}/api/test/members`, { grantsQuarterCredits })
            return ((await response.json()) as { memberId: string }).memberId
          },
          async setClock(now) {
            await post(`${apiUrl}/api/test/clock`, { now }, 'PUT')
          },
        })
      } finally {
        await Promise.all([stop(api), stop(spa)])
        await rm(dataDirectory, { recursive: true, force: true })
      }
    },
    { scope: 'worker', timeout: 90_000 },
  ],

  baseURL: async ({ backend }, provide) => {
    await provide(backend.appUrl)
  },
})

export { expect } from '@playwright/test'

/** Stand-in for sign-in until authentication exists: every request from this page names the Member. */
export async function signInAs(page: Page, memberId: string): Promise<void> {
  await page.context().setExtraHTTPHeaders({ [IDENTITY_HEADER]: memberId })
}
