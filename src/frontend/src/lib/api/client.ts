import { logger } from '@/lib/logger'

/**
 * A non-2xx response; `code` is the stable machine-readable ProblemDetails code (ADR 0006) and `traceId` the
 * reference a Member can quote, matching the backend log lines of that request (ADR 0010).
 */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly traceId: string

  constructor(status: number, code: string, title: string, traceId: string) {
    super(title)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.traceId = traceId
  }
}

interface Problem {
  title?: string
  code?: string
  traceId?: string
}

function randomHex(bytes: number): string {
  const values = crypto.getRandomValues(new Uint8Array(bytes))
  return Array.from(values, (value) => value.toString(16).padStart(2, '0')).join('')
}

/** A fresh W3C `traceparent` (version 00, sampled) for one call: 16 random bytes of trace id, 8 of span id. */
export function newTraceParent(): { header: string; traceId: string } {
  const traceId = randomHex(16)
  return { header: `00-${traceId}-${randomHex(8)}-01`, traceId }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
}

/**
 * Thin typed `fetch` wrapper: same-origin base URL, JSON in and out, ProblemDetails into ApiError. Every call
 * carries `X-Requested-With`, which the API requires on state-changing calls (CSRF, ADR 0006), and a fresh
 * `traceparent`; this is the only place that generates one (ADR 0010).
 */
export async function apiFetch<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const trace = newTraceParent()
  logger.setCurrentTraceId(trace.traceId)
  let response: Response
  try {
    response = await fetch(new URL(path, window.location.origin), {
      method: options.method ?? 'GET',
      headers: {
        accept: 'application/json',
        'x-requested-with': 'fetch',
        traceparent: trace.header,
        ...(options.body === undefined ? {} : { 'content-type': 'application/json' }),
      },
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
    })
  } catch (error) {
    // The message of a network failure can carry the URL, so only the code and trace id are reported.
    logger.error('Network request failed', { code: 'network_error', traceId: trace.traceId })
    throw error
  }
  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as Problem
    const apiError = new ApiError(
      response.status,
      problem.code ?? 'unknown',
      problem.title ?? response.statusText,
      problem.traceId ?? response.headers.get('x-trace-id') ?? trace.traceId,
    )
    // Expected 4xx outcomes are not errors (ADR 0010); the error object is marked so the boundary does not report it again.
    if (response.status >= 500) logger.reportError(apiError)
    throw apiError
  }
  if (response.status === 204 || response.status === 202) return undefined as T
  return (await response.json()) as T
}
