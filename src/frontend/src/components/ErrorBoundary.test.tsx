import { screen } from '@testing-library/react'
import { logger } from '@/lib/logger'
import { renderWithProviders } from '@/test/render'
import { ErrorBoundary } from './ErrorBoundary'

function Thrower(): never {
  throw new Error('Render failed')
}

describe('ErrorBoundary', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('shows the fallback and reports the render error once', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
    const reportError = vi.spyOn(logger, 'reportError')

    renderWithProviders(
      <ErrorBoundary>
        <Thrower />
      </ErrorBoundary>,
    )

    expect(screen.getByRole('alert')).toHaveTextContent('Something went wrong.')
    expect(screen.getByRole('button', { name: 'Reload' })).toBeInTheDocument()
    expect(reportError).toHaveBeenCalledOnce()
    expect(reportError).toHaveBeenCalledWith(
      expect.objectContaining({ message: 'Render failed' }),
      {
        code: 'render_error',
      },
    )
  })

  it('renders its children when nothing throws', () => {
    renderWithProviders(
      <ErrorBoundary>
        <p>Fine</p>
      </ErrorBoundary>,
    )

    expect(screen.getByText('Fine')).toBeInTheDocument()
  })
})
