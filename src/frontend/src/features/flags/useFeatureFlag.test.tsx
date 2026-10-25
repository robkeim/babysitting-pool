import { act, screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { vi } from 'vitest'
import { renderWithProviders } from '@/test/render'
import { server } from '@/test/server'
import { useFeatureFlag } from './api'
import { FeatureFlagsTestProvider } from './FeatureFlagsTestProvider'
import { featureFlagsHandler } from './mocks'

function Probe({ name }: { name: string }) {
  return <p>{useFeatureFlag(name) ? 'on' : 'off'}</p>
}

describe('useFeatureFlag', () => {
  it('reads the flag the server resolved for the caller', async () => {
    server.use(featureFlagsHandler({ alpha: true, beta: false }))
    renderWithProviders(
      <>
        <Probe name="alpha" />
        <Probe name="beta" />
      </>,
    )

    expect(await screen.findByText('on')).toBeInTheDocument()
    expect(screen.getByText('off')).toBeInTheDocument()
  })

  it('is off while loading, for an unknown name, and when the API fails', async () => {
    let requests = 0
    server.use(
      http.get('*/api/feature-flags', () => {
        requests += 1
        return new HttpResponse(null, { status: 500 })
      }),
    )
    renderWithProviders(<Probe name="alpha" />)

    expect(screen.getByText('off')).toBeInTheDocument()
    await vi.waitFor(() => {
      expect(requests).toBe(1)
    })
    expect(screen.getByText('off')).toBeInTheDocument()
  })

  it('picks up a change on the next refresh', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      let on = false
      server.use(http.get('*/api/feature-flags', () => HttpResponse.json({ alpha: on })))
      renderWithProviders(<Probe name="alpha" />)
      await vi.waitFor(() => {
        expect(screen.getByText('off')).toBeInTheDocument()
      })

      on = true
      await act(() => vi.advanceTimersByTimeAsync(60_000))

      expect(await screen.findByText('on')).toBeInTheDocument()
    } finally {
      vi.useRealTimers()
    }
  })

  it('uses the explicit values of the test provider without calling the API', () => {
    renderWithProviders(
      <FeatureFlagsTestProvider flags={{ alpha: true }}>
        <Probe name="alpha" />
        <Probe name="beta" />
      </FeatureFlagsTestProvider>,
    )

    expect(screen.getByText('on')).toBeInTheDocument()
    expect(screen.getByText('off')).toBeInTheDocument()
  })
})
