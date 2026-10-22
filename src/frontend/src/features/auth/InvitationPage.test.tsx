import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { vi } from 'vitest'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { InvitationPage } from './InvitationPage'
import { problem } from './mocks'

describe('InvitationPage', () => {
  it('shows who is invited, takes a name, and creates the account', async () => {
    let body: unknown
    server.use(
      http.post('*/api/invitations/preview', () => HttpResponse.json({ email: 'ada@example.com' })),
      http.post('*/api/invitations/accept', async ({ request }) => {
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
      http.get('*/api/me', () => problem(401, 'unauthenticated')),
    )
    const onJoined = vi.fn()
    renderWithProviders(<InvitationPage token="abc" onJoined={onJoined} />)

    expect(await screen.findByText('ada@example.com')).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Your name'), 'Ada')
    await userEvent.click(screen.getByRole('button', { name: 'Create my account' }))

    await waitFor(() => {
      expect(onJoined).toHaveBeenCalledTimes(1)
    })
    expect(body).toEqual({ token: 'abc', displayName: 'Ada' })
  })

  it('says the invitation cannot be used when it is unknown, used or expired', async () => {
    server.use(http.post('*/api/invitations/preview', () => problem(422, 'invitation_invalid')))

    renderWithProviders(<InvitationPage token="abc" onJoined={vi.fn()} />)

    expect(await screen.findByText(/invitation can't be used/)).toBeInTheDocument()
  })

  it('keeps the form and explains when the account cannot be created', async () => {
    server.use(
      http.post('*/api/invitations/preview', () => HttpResponse.json({ email: 'ada@example.com' })),
      http.post('*/api/invitations/accept', () => problem(400, 'invalid_request')),
    )
    const onJoined = vi.fn()
    renderWithProviders(<InvitationPage token="abc" onJoined={onJoined} />)

    await userEvent.type(await screen.findByLabelText('Your name'), 'Ada')
    await userEvent.click(screen.getByRole('button', { name: 'Create my account' }))

    expect(await screen.findByText(/couldn't create your account/)).toBeInTheDocument()
    expect(onJoined).not.toHaveBeenCalled()
  })
})
