/**
 * The SPA's error logger (ADR 0010): the console in development, batched `POST /api/client-logs` in production.
 * The endpoint accepts a fixed schema of short fields and no free-form objects, so every report is built here
 * from a sanitized message plus a stable code, the route name, the app version and a trace id.
 */

export interface ClientLog {
  message: string
  code: string
  route: string
  appVersion: string
  traceId?: string
}

interface LogContext {
  /** Stable machine-readable code (letters, digits, `_`, `.`, `-`); defaults to the error's name. */
  code?: string
  /** The trace id of the failed call; defaults to the current one (the latest API call's). */
  traceId?: string
}

export interface LoggerOptions {
  /** Production batches to the backend; otherwise entries go to the console. */
  production: boolean
  appVersion: string
  /** The current route's name, never its concrete path (ids, tokens). */
  getRoute: () => string
  flushIntervalMs?: number
  maxBatch?: number
  maxQueue?: number
}

const MAX_MESSAGE_LENGTH = 500
const MAX_CODE_LENGTH = 64
const MAX_ROUTE_LENGTH = 100
const UNKNOWN = 'unknown'

/** The message is client text and the endpoint does not mask it: no URLs, emails or control characters (ADR 0010). */
export function sanitizeMessage(raw: string): string {
  const cleaned = raw
    .replace(/[a-z][a-z0-9+.-]*:\/\/\S+/gi, '[url]')
    .replace(/\S+@\S+/g, '[email]')
    .replace(/[\p{Cc}\p{Cf}\p{Zl}\p{Zp}\s]+/gu, ' ')
    .trim()
  return (cleaned || UNKNOWN).slice(0, MAX_MESSAGE_LENGTH)
}

function sanitizeToken(raw: string, allowed: RegExp, maxLength: number): string {
  return raw.replace(allowed, '_').slice(0, maxLength) || UNKNOWN
}

function describe(error: unknown): { message: string; code: string; traceId?: string } {
  if (error instanceof Error) {
    const carried = error as Error & { code?: unknown; traceId?: unknown }
    return {
      message: error.message,
      code: typeof carried.code === 'string' ? carried.code : error.name,
      traceId: typeof carried.traceId === 'string' ? carried.traceId : undefined,
    }
  }
  return {
    message: typeof error === 'string' ? error : 'Non-error value thrown',
    code: 'non_error',
  }
}

export class Logger {
  private readonly options: Required<LoggerOptions>
  private queue: ClientLog[] = []
  private timer: ReturnType<typeof setTimeout> | undefined
  private currentTraceId: string | undefined
  private readonly reported = new WeakSet<object>()

  constructor(options: LoggerOptions) {
    this.options = { flushIntervalMs: 2000, maxBatch: 10, maxQueue: 20, ...options }
  }

  /** Called by the API wrapper per call, so an error outside a call still carries the latest trace id. */
  setCurrentTraceId(traceId: string) {
    this.currentTraceId = traceId
  }

  /** Reports a caught value once: the same error object seen again (an ApiError already logged) is skipped. */
  reportError(error: unknown, context: LogContext = {}) {
    if (typeof error === 'object' && error !== null) {
      if (this.reported.has(error)) return
      this.reported.add(error)
    }
    const described = describe(error)
    this.error(described.message, {
      code: context.code ?? described.code,
      traceId: context.traceId ?? described.traceId,
    })
  }

  error(message: string, context: LogContext = {}) {
    if (!this.options.production) {
      console.error(message, context)
      return
    }
    this.enqueue({
      message: sanitizeMessage(message),
      code: sanitizeToken(context.code ?? UNKNOWN, /[^A-Za-z0-9_.-]/g, MAX_CODE_LENGTH),
      route: sanitizeToken(this.options.getRoute(), /[^A-Za-z0-9_./:-]/g, MAX_ROUTE_LENGTH),
      appVersion: sanitizeToken(this.options.appVersion, /[^A-Za-z0-9_.+-]/g, 32),
      traceId: this.validTraceId(context.traceId ?? this.currentTraceId),
    })
  }

  /** Development only: the backend log takes errors, and nothing else is worth a request in production. */
  warn(message: string) {
    if (!this.options.production) console.warn(message)
  }

  /** Development only, like `warn`. */
  info(message: string) {
    if (!this.options.production) console.info(message)
  }

  /** Sends what is queued now; `keepalive` lets the requests outlive a closing page. */
  flush() {
    clearTimeout(this.timer)
    this.timer = undefined
    const batch = this.queue.splice(0)
    for (const entry of batch) {
      const { traceId, ...rest } = entry
      // Plain fetch, not the API wrapper: a failing logger must never log or recurse.
      void fetch('/api/client-logs', {
        method: 'POST',
        keepalive: true,
        headers: { 'content-type': 'application/json', 'x-requested-with': 'fetch' },
        body: JSON.stringify(traceId === undefined ? rest : { ...rest, traceId }),
      }).catch(() => undefined)
    }
  }

  private enqueue(entry: ClientLog) {
    this.queue.push(entry)
    // Drop the oldest on overflow, as the backend sink does.
    if (this.queue.length > this.options.maxQueue) this.queue.shift()
    if (this.queue.length >= this.options.maxBatch) this.flush()
    else {
      this.timer ??= setTimeout(() => {
        this.flush()
      }, this.options.flushIntervalMs)
    }
  }

  private validTraceId(traceId: string | undefined): string | undefined {
    return traceId !== undefined && /^[0-9a-f]{32}$/.test(traceId) ? traceId : undefined
  }
}

/**
 * Wires uncaught exceptions, unhandled rejections and page hide (final flush) to the logger.
 * Returns the function that removes the listeners.
 */
export function installGlobalHandlers(logger: Logger, target: Window = window): () => void {
  const onError = (event: ErrorEvent) => {
    logger.reportError(event.error ?? new Error(event.message), { code: 'uncaught_exception' })
  }
  const onRejection = (event: PromiseRejectionEvent) => {
    logger.reportError(event.reason, { code: 'unhandled_rejection' })
  }
  const onPageHide = () => {
    logger.flush()
  }
  target.addEventListener('error', onError)
  target.addEventListener('unhandledrejection', onRejection)
  target.addEventListener('pagehide', onPageHide)
  return () => {
    target.removeEventListener('error', onError)
    target.removeEventListener('unhandledrejection', onRejection)
    target.removeEventListener('pagehide', onPageHide)
  }
}
