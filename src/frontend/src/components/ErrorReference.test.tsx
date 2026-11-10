import { screen } from '@testing-library/react'
import { ApiError } from '@/lib/api/client'
import { renderWithProviders } from '@/test/render'
import { ErrorReference } from './ErrorReference'

describe('ErrorReference', () => {
  it('shows the trace id of a failed call as a reference', () => {
    renderWithProviders(<ErrorReference error={new ApiError(500, 'x', 'Boom', 'abc123')} />)

    expect(screen.getByText('abc123')).toBeInTheDocument()
    expect(screen.getByText(/reference/i)).toBeInTheDocument()
  })

  it('shows nothing for an error that is not an ApiError', () => {
    const { container } = renderWithProviders(<ErrorReference error={new Error('x')} />)

    expect(container).toBeEmptyDOMElement()
  })
})
