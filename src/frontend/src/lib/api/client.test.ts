import { http, HttpResponse } from 'msw'
import { logger } from '@/lib/logger'
import { server } from '@/test/server'
import { ApiError, apiFetch } from './client'

const TRACEPARENT = /^00-([0-9a-f]{32})-[0-9a-f]{16}-01$/

describe('apiFetch', () => {
  it('sends a fresh W3C traceparent on every call', async () => {
    const seen: string[] = []
    server.use(
      http.get('*/api/ping', ({ request }) => {
        seen.push(request.headers.get('traceparent') ?? '')
        return HttpResponse.json({})
      }),
    )

    await apiFetch('/api/ping')
    await apiFetch('/api/ping')

    expect(seen).toHaveLength(2)
    expect(seen[0]).toMatch(TRACEPARENT)
    expect(seen[1]).toMatch(TRACEPARENT)
    expect(seen[0]).not.toEqual(seen[1])
  })

  it('puts the TraceId of the Problem body on the ApiError', async () => {
    server.use(
      http.get('*/api/ping', () =>
        HttpResponse.json(
          { status: 500, title: 'Boom', code: 'internal_error', traceId: 'a'.repeat(32) },
          { status: 500 },
        ),
      ),
    )

    const error = await apiFetch('/api/ping').catch((caught: unknown) => caught)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).traceId).toBe('a'.repeat(32))
  })

  it('falls back to the trace id it sent when the body has none', async () => {
    let sent = ''
    server.use(
      http.get('*/api/ping', ({ request }) => {
        sent = TRACEPARENT.exec(request.headers.get('traceparent') ?? '')?.[1] ?? ''
        return new HttpResponse('nope', { status: 502 })
      }),
    )

    const error = await apiFetch('/api/ping').catch((caught: unknown) => caught)

    expect(sent).not.toBe('')
    expect((error as ApiError).traceId).toBe(sent)
  })

  describe('error logging', () => {
    afterEach(() => {
      vi.restoreAllMocks()
    })

    it('logs a 5xx once, with its code and trace id', async () => {
      const reportError = vi.spyOn(logger, 'reportError').mockImplementation(() => undefined)
      server.use(
        http.get('*/api/ping', () =>
          HttpResponse.json(
            { status: 500, title: 'Boom', code: 'internal_error', traceId: 'a'.repeat(32) },
            { status: 500 },
          ),
        ),
      )

      const error = await apiFetch('/api/ping').catch((caught: unknown) => caught)

      expect(reportError).toHaveBeenCalledOnce()
      expect(reportError).toHaveBeenCalledWith(error)
    })

    it('does not log an expected 4xx outcome', async () => {
      const reportError = vi.spyOn(logger, 'reportError').mockImplementation(() => undefined)
      server.use(
        http.get('*/api/ping', () =>
          HttpResponse.json({ status: 409, title: 'Nope', code: 'conflict' }, { status: 409 }),
        ),
      )

      await apiFetch('/api/ping').catch(() => undefined)

      expect(reportError).not.toHaveBeenCalled()
    })

    it('logs a network failure with the trace id it sent', async () => {
      const reportError = vi.spyOn(logger, 'reportError').mockImplementation(() => undefined)
      let sent = ''
      server.use(
        http.get('*/api/ping', ({ request }) => {
          sent = TRACEPARENT.exec(request.headers.get('traceparent') ?? '')?.[1] ?? ''
          return HttpResponse.error()
        }),
      )

      await expect(apiFetch('/api/ping')).rejects.toThrow()

      expect(reportError).toHaveBeenCalledWith(expect.any(TypeError), {
        code: 'network_error',
        traceId: sent,
      })
    })

    it('gives the logger the latest trace id for errors outside a call', async () => {
      const setCurrentTraceId = vi.spyOn(logger, 'setCurrentTraceId')
      server.use(http.get('*/api/ping', () => HttpResponse.json({})))

      await apiFetch('/api/ping')

      expect(setCurrentTraceId).toHaveBeenCalledWith(expect.stringMatching(/^[0-9a-f]{32}$/))
    })
  })
})
