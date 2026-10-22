import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { vi } from 'vitest'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { problem } from './mocks'
import { SignInVerifyPage } from './SignInVerifyPage'

describe('SignInVerifyPage', () => {
  it('exchanges the token for a session once and continues', async () => {
    const requests: unknown[] = []
    server.use(
      http.post('*/api/auth/sessions', async ({ request }) => {
        requests.push(await request.json())
        return new HttpResponse(null, { status: 204 })
      }),
      http.get('*/api/me', () => problem(401, 'unauthenticated')),
    )
    const onSignedIn = vi.fn()

    renderWithProviders(<SignInVerifyPage token="abc" onSignedIn={onSignedIn} />)

    await waitFor(() => {
      expect(onSignedIn).toHaveBeenCalledTimes(1)
    })
    expect(requests).toEqual([{ token: 'abc' }])
  })

  it('offers a new link when the token is rejected', async () => {
    server.use(http.post('*/api/auth/sessions', () => problem(422, 'sign_in_link_invalid')))
    const onSignedIn = vi.fn()

    renderWithProviders(<SignInVerifyPage token="abc" onSignedIn={onSignedIn} />)

    expect(await screen.findByText(/can't be used/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Request a new one.' })).toHaveAttribute(
      'href',
      '/sign-in',
    )
    expect(onSignedIn).not.toHaveBeenCalled()
  })

  it('does not call the API without a token', () => {
    renderWithProviders(<SignInVerifyPage token={undefined} onSignedIn={vi.fn()} />)

    expect(screen.getByText(/can't be used/)).toBeInTheDocument()
  })
})
