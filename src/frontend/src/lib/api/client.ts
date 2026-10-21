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

/** Thin typed `fetch` wrapper: same-origin base URL, JSON in and out, ProblemDetails into ApiError. */
export async function apiFetch<T>(path: string): Promise<T> {
  const response = await fetch(new URL(path, window.location.origin), {
    headers: { accept: 'application/json' },
  })
  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as Problem
    throw new ApiError(
      response.status,
      problem.code ?? 'unknown',
      problem.title ?? response.statusText,
    )
  }
  return (await response.json()) as T
}
