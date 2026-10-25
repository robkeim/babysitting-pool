import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { InvitationsAdminPage } from './InvitationsAdminPage'
import {
  inviteHandler,
  inviteRejectedHandler,
  invitationsFailureHandler,
  invitationsHandler,
  resendHandler,
  resendRejectedHandler,
  sampleInvitation,
} from './mocks'

describe('InvitationsAdminPage', () => {
  it('lists each invitation with its state', async () => {
    server.use(
      invitationsHandler([
        sampleInvitation,
        { ...sampleInvitation, id: '2', email: 'ben@example.com', status: 'Expired' },
        {
          ...sampleInvitation,
          id: '3',
          email: 'cleo@example.com',
          status: 'Accepted',
          acceptedAt: '2026-03-02T10:00:00Z',
        },
      ]),
    )

    renderWithProviders(<InvitationsAdminPage />)

    const pending = await screen.findByRole('group', { name: 'ada@example.com' })
    expect(within(pending).getByText('Pending')).toBeInTheDocument()
    expect(within(pending).getByText(/Valid until/)).toBeInTheDocument()
    expect(
      within(screen.getByRole('group', { name: 'ben@example.com' })).getByText('Expired'),
    ).toBeInTheDocument()
    const accepted = screen.getByRole('group', { name: 'cleo@example.com' })
    expect(within(accepted).getByText('Accepted')).toBeInTheDocument()
    expect(within(accepted).queryByRole('button')).not.toBeInTheDocument()
  })

  it('says so when there are no invitations', async () => {
    server.use(invitationsHandler([]))

    renderWithProviders(<InvitationsAdminPage />)

    expect(await screen.findByText('No invitations have been sent yet.')).toBeInTheDocument()
  })

  it('says so when the list fails to load', async () => {
    server.use(invitationsFailureHandler())

    renderWithProviders(<InvitationsAdminPage />)

    expect(await screen.findByText("We couldn't load the invitations")).toBeInTheDocument()
  })

  it('invites a person by email and clears the form', async () => {
    const user = userEvent.setup()
    const invited: string[] = []
    server.use(
      invitationsHandler([]),
      inviteHandler((email) => invited.push(email)),
    )

    renderWithProviders(<InvitationsAdminPage />)
    await user.type(await screen.findByLabelText('Email address'), 'new@example.com')
    await user.click(screen.getByRole('button', { name: 'Send invitation' }))

    expect(await screen.findByRole('status')).toHaveTextContent(
      'Invitation sent to new@example.com.',
    )
    expect(invited).toEqual(['new@example.com'])
    expect(screen.getByLabelText('Email address')).toHaveValue('')
  })

  it('explains why an invitation was refused', async () => {
    const user = userEvent.setup()
    server.use(invitationsHandler([]), inviteRejectedHandler(422, 'already_a_member'))

    renderWithProviders(<InvitationsAdminPage />)
    await user.type(await screen.findByLabelText('Email address'), 'ada@example.com')
    await user.click(screen.getByRole('button', { name: 'Send invitation' }))

    expect(await screen.findByText('This address already belongs to a member.')).toBeInTheDocument()
  })

  it('re-sends a pending invitation', async () => {
    const user = userEvent.setup()
    const resent: string[] = []
    server.use(
      invitationsHandler([sampleInvitation]),
      resendHandler((id) => resent.push(id)),
    )

    renderWithProviders(<InvitationsAdminPage />)
    await user.click(
      await screen.findByRole('button', { name: 'Re-send the invitation to ada@example.com' }),
    )

    expect(await screen.findByRole('status')).toHaveTextContent('A new link was sent.')
    expect(resent).toEqual([sampleInvitation.id])
  })

  it('explains why a re-send was refused', async () => {
    const user = userEvent.setup()
    server.use(
      invitationsHandler([sampleInvitation]),
      resendRejectedHandler(422, 'invitation_already_accepted'),
    )

    renderWithProviders(<InvitationsAdminPage />)
    await user.click(await screen.findByRole('button', { name: /Re-send/ }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'That invitation was already accepted.',
    )
  })
})
