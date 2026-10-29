import { execFileSync } from 'node:child_process'
import { mkdtemp, readFile, rm } from 'node:fs/promises'
import { createServer, type Server } from 'node:http'
import { Readable } from 'node:stream'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { FRONTEND_ROOT } from './paths.ts'
import { expect, test } from './fixtures.ts'

const CONTENT_TYPES: Record<string, string> = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript',
  '.css': 'text/css',
  '.woff2': 'font/woff2',
  '.svg': 'image/svg+xml',
}

let buildDirectory: string

// The dev server injects inline scripts, so the CSP is checked against the production build (ADR 0014).
test.beforeAll(async () => {
  buildDirectory = await mkdtemp(path.join(tmpdir(), 'babysitting-spa-'))
  execFileSync(
    path.join(FRONTEND_ROOT, 'node_modules/.bin/vite'),
    ['build', '--outDir', buildDirectory, '--emptyOutDir'],
    { cwd: FRONTEND_ROOT, stdio: 'inherit' },
  )
})

test.afterAll(async () => {
  await rm(buildDirectory, { recursive: true, force: true })
})

/** Serves the built SPA and proxies `/api`, sending the API's own CSP with every page, as one host will in production. */
async function serveBuild(apiUrl: string, csp: string): Promise<{ url: string; server: Server }> {
  const server = createServer((request, response) => {
    void (async () => {
      const url = new URL(request.url ?? '/', 'http://localhost')
      if (url.pathname.startsWith('/api')) {
        const chunks: Buffer[] = []
        for await (const chunk of request) chunks.push(chunk as Buffer)
        // The Notification stream never ends, so the body is piped through and dropped when the browser leaves.
        const aborted = new AbortController()
        response.on('close', () => {
          if (!response.writableFinished) aborted.abort()
        })
        // Aborting before the headers arrive rejects the fetch; the browser has gone, so there is nobody to answer.
        const upstream = await fetch(apiUrl + url.pathname + url.search, {
          signal: aborted.signal,
          method: request.method,
          headers: request.headers as Record<string, string>,
          body: chunks.length > 0 ? Buffer.concat(chunks) : undefined,
        }).catch(() => undefined)
        if (!upstream) {
          response.destroy()
          return
        }
        const headers = Object.fromEntries(upstream.headers)
        delete headers['content-encoding']
        delete headers['content-length']
        delete headers['transfer-encoding']
        response.writeHead(upstream.status, headers)
        if (upstream.body) {
          Readable.fromWeb(upstream.body)
            .on('error', () => {
              response.destroy()
            })
            .pipe(response)
        } else {
          response.end()
        }
        return
      }

      const file = path.join(
        buildDirectory,
        path.extname(url.pathname) ? url.pathname : 'index.html',
      )
      try {
        const body = await readFile(file)
        response.writeHead(200, {
          'content-type': CONTENT_TYPES[path.extname(file)] ?? 'application/octet-stream',
          'content-security-policy': csp,
        })
        response.end(body)
      } catch {
        response.writeHead(404).end()
      }
    })()
  })
  await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve))
  const { port } = server.address() as { port: number }
  return { url: `http://127.0.0.1:${port}`, server }
}

test('the SPA loads and works under the API CSP without violations', async ({ page, backend }) => {
  const csp = (await fetch(`${backend.apiUrl}/health`)).headers.get('content-security-policy')
  expect(csp).toContain("default-src 'self'")

  const { url, server } = await serveBuild(backend.apiUrl, csp ?? '')
  try {
    const violations: string[] = []
    page.on('console', (message) => {
      if (/content security policy/i.test(message.text())) violations.push(message.text())
    })

    await backend.setClock('2026-03-01T09:00:00Z')
    const memberId = await backend.seedMember([40])
    const response = await fetch(`${backend.apiUrl}/api/test/sessions`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ memberId }),
    })
    const { token } = (await response.json()) as { token: string }
    await page.context().addCookies([{ name: 'session', value: token, url }])

    await page.goto(`${url}/credits`)

    await expect(page.getByRole('group', { name: 'Available' })).toContainText('10.00')
    expect(violations).toEqual([])
  } finally {
    server.closeAllConnections()
    await new Promise((resolve) => server.close(resolve))
  }
})
