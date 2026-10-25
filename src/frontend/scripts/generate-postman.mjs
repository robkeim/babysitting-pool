// Generates docs/api/postman_collection.json from docs/api/openapi.json (ADR 0006).
// The converter emits random ids and picks random enum examples; ids are stripped and every enum is narrowed to
// its first value, and a free-form map (additionalProperties, no properties) becomes an object with one fixed key, in the
// copy handed to the converter, so the output is deterministic and drift-checkable.
import { execFileSync } from 'node:child_process'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

const spec = new URL('../../../docs/api/openapi.json', import.meta.url).pathname
const target = new URL('../../../docs/api/postman_collection.json', import.meta.url).pathname
const dir = mkdtempSync(join(tmpdir(), 'postman-'))
const raw = join(dir, 'collection.json')
const pinnedSpec = join(dir, 'openapi.json')

const pinEnums = (node) => {
  if (Array.isArray(node)) return node.map(pinEnums)
  if (node && typeof node === 'object') {
    const pinned = Object.fromEntries(
      Object.entries(node).map(([key, value]) => [key, pinEnums(value)]),
    )
    if (Array.isArray(pinned.enum)) pinned.enum = pinned.enum.slice(0, 1)
    if (!pinned.properties && pinned.additionalProperties?.type) {
      pinned.properties = { key: pinned.additionalProperties }
      delete pinned.additionalProperties
    }
    return pinned
  }
  return node
}

try {
  writeFileSync(pinnedSpec, JSON.stringify(pinEnums(JSON.parse(readFileSync(spec, 'utf8')))))
  execFileSync('npx', ['--yes', 'openapi-to-postmanv2@5.0.0', '-s', pinnedSpec, '-o', raw, '-p'], {
    stdio: 'inherit',
  })
  const strip = (node) => {
    if (Array.isArray(node)) return node.map(strip)
    if (node && typeof node === 'object') {
      return Object.fromEntries(
        Object.entries(node)
          .filter(([key]) => key !== 'id' && key !== '_postman_id')
          .map(([key, value]) => [key, strip(value)]),
      )
    }
    return node
  }
  const collection = strip(JSON.parse(readFileSync(raw, 'utf8')))
  collection.info = { ...collection.info, name: 'Babysitting Pool API' }
  // The Vite origin, not the API port: cookies and the /api proxy then behave as they do in the browser.
  collection.variable = [{ key: 'baseUrl', value: 'http://localhost:5173', type: 'string' }]
  // CSRF defence (ADR 0006): state-changing calls need this header.
  collection.event = [
    {
      listen: 'prerequest',
      script: {
        type: 'text/javascript',
        exec: ['pm.request.headers.upsert({ key: "X-Requested-With", value: "postman" });'],
      },
    },
  ]
  writeFileSync(target, `${JSON.stringify(collection, null, 2)}\n`)
} finally {
  rmSync(dir, { recursive: true, force: true })
}
