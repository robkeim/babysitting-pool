import { http, HttpResponse } from 'msw'
import { server } from '@/test/server'
import { installGlobalHandlers, Logger, sanitizeMessage } from './logger'

const TRACE_ID = 'ab'.repeat(16)

function productionLogger() {
  return new Logger({ production: true, appVersion: '1.2.3', getRoute: () => '/_authed/credits' })
}

function captureReports() {
  const reports: Record<string, unknown>[] = []
  server.use(
    http.post('*/api/client-logs', async ({ request }) => {
      reports.push((await request.json()) as Record<string, unknown>)
      return new HttpResponse(null, { status: 204 })
    }),
  )
  return reports
}

describe('Logger', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
  })
  afterEach(() => {
    vi.useRealTimers()
    vi.restoreAllMocks()
  })

  it('writes to the console in development and sends nothing', () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => undefined)
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const logger = new Logger({ production: false, appVersion: 'dev', getRoute: () => 'x' })

    logger.error('Boom', { code: 'render_error' })
    logger.warn('Careful')
    logger.flush()

    expect(error).toHaveBeenCalledOnce()
    expect(warn).toHaveBeenCalledWith('Careful')
  })

  it('sends the fixed payload shape and nothing else', async () => {
    const reports = captureReports()
    const logger = productionLogger()

    logger.error('Boom', { code: 'render_error', traceId: TRACE_ID })
    logger.flush()

    await vi.waitFor(() => {
      expect(reports).toEqual([
        {
          message: 'Boom',
          code: 'render_error',
          route: '/_authed/credits',
          appVersion: '1.2.3',
          traceId: TRACE_ID,
        },
      ])
    })
  })

  it('omits the trace id when there is none, and falls back to the current one', async () => {
    const reports = captureReports()
    const logger = productionLogger()

    logger.error('Before any call', { code: 'a' })
    logger.setCurrentTraceId(TRACE_ID)
    logger.error('After a call', { code: 'b' })
    logger.flush()

    await vi.waitFor(() => {
      expect(reports).toHaveLength(2)
    })
    expect(reports.find((report) => report.message === 'Before any call')).not.toHaveProperty(
      'traceId',
    )
    expect(reports.find((report) => report.message === 'After a call')).toHaveProperty(
      'traceId',
      TRACE_ID,
    )
  })

  it('batches: nothing is sent until the interval, then everything queued goes at once', async () => {
    const reports = captureReports()
    const logger = productionLogger()

    logger.error('one', { code: 'a' })
    logger.error('two', { code: 'a' })
    await vi.advanceTimersByTimeAsync(1999)
    expect(reports).toHaveLength(0)

    await vi.advanceTimersByTimeAsync(1)
    await vi.waitFor(() => {
      expect(reports.map((report) => report.message).sort()).toEqual(['one', 'two'])
    })
  })

  it('flushes early when a batch is full', async () => {
    const reports = captureReports()
    const logger = new Logger({
      production: true,
      appVersion: '1',
      getRoute: () => 'x',
      maxBatch: 2,
    })

    logger.error('one', { code: 'a' })
    logger.error('two', { code: 'a' })

    await vi.waitFor(() => {
      expect(reports.map((report) => report.message).sort()).toEqual(['one', 'two'])
    })
  })

  it('drops the oldest entry when the queue overflows', async () => {
    const reports = captureReports()
    const logger = new Logger({
      production: true,
      appVersion: '1',
      getRoute: () => 'x',
      maxQueue: 2,
    })

    logger.error('one', { code: 'a' })
    logger.error('two', { code: 'a' })
    logger.error('three', { code: 'a' })
    logger.flush()

    await vi.waitFor(() => {
      expect(reports.map((report) => report.message).sort()).toEqual(['three', 'two'])
    })
  })

  it('never throws or recurses when the endpoint fails', async () => {
    let attempts = 0
    server.use(
      http.post('*/api/client-logs', () => {
        attempts += 1
        return HttpResponse.error()
      }),
    )
    const logger = productionLogger()

    logger.error('Boom', { code: 'a' })
    expect(() => {
      logger.flush()
    }).not.toThrow()

    await vi.waitFor(() => {
      expect(attempts).toBe(1)
    })
  })

  it('reports the same error object once', async () => {
    const reports = captureReports()
    const logger = productionLogger()
    const error = new Error('Boom')

    logger.reportError(error)
    logger.reportError(error)
    logger.flush()

    await vi.waitFor(() => {
      expect(reports).toHaveLength(1)
    })
    expect(reports[0]).toMatchObject({ message: 'Boom', code: 'Error' })
  })

  it('takes the code and trace id an error carries', async () => {
    const reports = captureReports()
    const logger = productionLogger()
    const error = Object.assign(new Error('Server broke'), {
      code: 'internal_error',
      traceId: TRACE_ID,
    })

    logger.reportError(error)
    logger.flush()

    await vi.waitFor(() => {
      expect(reports[0]).toMatchObject({ code: 'internal_error', traceId: TRACE_ID })
    })
  })

  it('coerces codes and routes into the endpoint charsets', async () => {
    const reports = captureReports()
    const logger = new Logger({
      production: true,
      appVersion: '1.0 beta',
      getRoute: () => '/requests/$requestId',
    })

    logger.error('Boom', { code: 'bad code!' })
    logger.flush()

    await vi.waitFor(() => {
      expect(reports[0]).toMatchObject({
        code: 'bad_code_',
        route: '/requests/_requestId',
        appVersion: '1.0_beta',
      })
    })
  })

  it('drops a trace id that is not 32 lowercase hex characters', async () => {
    const reports = captureReports()
    const logger = productionLogger()

    logger.error('Boom', { code: 'a', traceId: 'NOT-HEX' })
    logger.flush()

    await vi.waitFor(() => {
      expect(reports).toHaveLength(1)
    })
    expect(reports[0]).not.toHaveProperty('traceId')
  })
})

describe('sanitizeMessage', () => {
  it('strips URLs and emails, control characters and line breaks, and caps the length', () => {
    expect(sanitizeMessage('Failed https://x.test/a?token=1 for ann@example.com\nnow‮!')).toBe(
      'Failed [url] for [email] now !',
    )
    expect(sanitizeMessage('x'.repeat(600))).toHaveLength(500)
    expect(sanitizeMessage('  \n ')).toBe('unknown')
  })
})

describe('installGlobalHandlers', () => {
  it('reports uncaught exceptions and unhandled rejections, and stops after removal', () => {
    const logger = productionLogger()
    const reportError = vi.spyOn(logger, 'reportError')
    // A bare EventTarget: dispatching an ErrorEvent on jsdom's window would also trip Vitest's own error listener.
    const target = new EventTarget() as Window
    const remove = installGlobalHandlers(logger, target)

    const boom = new Error('Boom')
    target.dispatchEvent(new ErrorEvent('error', { error: boom, message: 'Boom' }))
    const rejection = new Event('unhandledrejection')
    Object.assign(rejection, { reason: 'nope' })
    target.dispatchEvent(rejection)

    expect(reportError).toHaveBeenNthCalledWith(1, boom, { code: 'uncaught_exception' })
    expect(reportError).toHaveBeenNthCalledWith(2, 'nope', { code: 'unhandled_rejection' })

    remove()
    target.dispatchEvent(new ErrorEvent('error', { error: boom }))
    expect(reportError).toHaveBeenCalledTimes(2)
  })

  it('flushes when the page is hidden', () => {
    const logger = productionLogger()
    const flush = vi.spyOn(logger, 'flush')
    const target = new EventTarget() as Window
    const remove = installGlobalHandlers(logger, target)

    target.dispatchEvent(new Event('pagehide'))

    expect(flush).toHaveBeenCalledOnce()
    remove()
  })
})
