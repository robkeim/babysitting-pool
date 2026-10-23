import { test as base, type BrowserContext } from '@playwright/test'
import { spawn, type ChildProcess } from 'node:child_process'
import { mkdtemp, rm } from 'node:fs/promises'
import { createServer } from 'node:net'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { API_DLL, API_PROJECT, FRONTEND_ROOT } from './paths.ts'

/** The address the API invites as Admin at startup; its Invitation is the entry point for the sign-in journey. */
export const BOOTSTRAP_ADMIN_EMAIL = 'root@example.test'

/** One worker's private stack: its own API process, SQLite file, fake clock and SPA dev server. */
export interface Backend {
  /** The SPA origin; it proxies `/api` to this worker's API, so the browser sees one origin (ADR 0006). */
  appUrl: string
  /** Seeds a Member whose Credit Account holds the given Grants (quarter-credits, ADR 0001). */
  seedMember(grantsQuarterCredits: number[]): Promise<string>
  /** The newest link (Invitation or sign-in) emailed to the address, read from the fake outbox. */
  latestLink(email: string): Promise<string>
  /** Signs the browser context in as a seeded Member without going through email. */
  signIn(context: BrowserContext, memberId: string): Promise<void>
  /** Sets the server clock to an absolute instant. */
  setClock(now: string): Promise<void>
  /** Runs Settle, the way the hosted service does on its tick (switched off in E2E). */
  settle(): Promise<void>
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
          App__BaseUrl: appUrl,
          Bootstrap__AdminEmail: BOOTSTRAP_ADMIN_EMAIL,
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
          async latestLink(email) {
            const response = await fetch(`${apiUrl}/api/dev/outbox?to=${encodeURIComponent(email)}`)
            const messages = (await response.json()) as { link: string }[]
            const latest = messages.at(-1)
            if (!latest) throw new Error(`No email was sent to ${email}`)
            return latest.link
          },
          async signIn(context, memberId) {
            const response = await post(`${apiUrl}/api/test/sessions`, { memberId })
            const { token } = (await response.json()) as { token: string }
            await context.addCookies([{ name: 'session', value: token, url: appUrl }])
          },
          async setClock(now) {
            await post(`${apiUrl}/api/test/clock`, { now }, 'PUT')
          },
          async settle() {
            await post(`${apiUrl}/api/test/settle`, {})
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
