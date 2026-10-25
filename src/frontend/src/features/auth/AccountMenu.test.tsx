import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { vi } from 'vitest'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { AccountMenu } from './AccountMenu'
import { meHandler, signedOutHandler } from './mocks'
import { RequireMember } from './RequireMember'

const ada = {
  memberId: '2f6c1c3e-5d8a-4a38-9b1d-0a7a1c1b9e11',
  email: 'ada@example.com',
  displayName: 'Ada',
  language: 'en-US',
  isAdmin: false,
}

describe('AccountMenu', () => {
  it('shows the Member and signs them out', async () => {
    server.use(
      meHandler(ada),
      http.delete('*/api/auth/sessions/current', () => new HttpResponse(null, { status: 204 })),
    )
    const onSignedOut = vi.fn()
    renderWithProviders(<AccountMenu onSignedOut={onSignedOut} />)

    expect(await screen.findByText('Ada')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Sign out' }))

    await waitFor(() => {
      expect(onSignedOut).toHaveBeenCalledTimes(1)
    })
  })

  it('saves the Language the Member picks and shows it selected', async () => {
    let saved: unknown
    server.use(
      meHandler(ada),
      http.put('*/api/me/language', async ({ request }) => {
        saved = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderWithProviders(<AccountMenu onSignedOut={vi.fn()} />)

    const dropdown = await screen.findByRole('combobox', { name: 'Language' })
    await userEvent.selectOptions(dropdown, '🏴‍☠️ Ünïcödé Tëst')

    await waitFor(() => {
      expect(dropdown).toHaveValue('en-XA')
    })
    expect(saved).toEqual({ language: 'en-XA' })
  })

  it('renders nothing when nobody is signed in', async () => {
    server.use(signedOutHandler())

    const { container } = renderWithProviders(<AccountMenu onSignedOut={vi.fn()} />)

    await waitFor(() => {
      expect(container).toBeEmptyDOMElement()
    })
  })
})

describe('RequireMember', () => {
  it('shows the page to a signed-in Member', async () => {
    server.use(meHandler(ada))

    renderWithProviders(
      <RequireMember signedOut={<p>Please sign in</p>}>Secret page</RequireMember>,
    )

    expect(await screen.findByText('Secret page')).toBeInTheDocument()
  })

  it('shows the signed-out fallback when there is no session', async () => {
    server.use(signedOutHandler())

    renderWithProviders(
      <RequireMember signedOut={<p>Please sign in</p>}>Secret page</RequireMember>,
    )

    expect(await screen.findByText('Please sign in')).toBeInTheDocument()
    expect(screen.queryByText('Secret page')).not.toBeInTheDocument()
  })
})
