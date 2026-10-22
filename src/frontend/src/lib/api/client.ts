/** A non-2xx response; `code` is the stable machine-readable ProblemDetails code (ADR 0006). */
export class ApiError extends Error {
  readonly status: number
  readonly code: string

  constructor(status: number, code: string, title: string) {
    super(title)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

interface Problem {
  title?: string
  code?: string
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
}

/**
 * Thin typed `fetch` wrapper: same-origin base URL, JSON in and out, ProblemDetails into ApiError. Every call
 * carries `X-Requested-With`, which the API requires on state-changing calls (CSRF, ADR 0006).
 */
export async function apiFetch<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await fetch(new URL(path, window.location.origin), {
    method: options.method ?? 'GET',
    headers: {
      accept: 'application/json',
      'x-requested-with': 'fetch',
      ...(options.body === undefined ? {} : { 'content-type': 'application/json' }),
    },
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  })
  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as Problem
    throw new ApiError(
      response.status,
      problem.code ?? 'unknown',
      problem.title ?? response.statusText,
    )
  }
  if (response.status === 204 || response.status === 202) return undefined as T
  return (await response.json()) as T
}
