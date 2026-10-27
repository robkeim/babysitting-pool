import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { MembersAdminPage } from './MembersAdminPage'
import {
  eraseHandler,
  eraseRejectedHandler,
  membersFailureHandler,
  membersHandler,
  offboardHandler,
  offboardRejectedHandler,
  sampleMember,
} from './mocks'

const admin = {
  ...sampleMember,
  id: '2',
  displayName: 'Rosa Root',
  email: 'rosa@example.com',
  isAdmin: true,
}
const closed = {
  ...sampleMember,
  id: '3',
  displayName: 'Ben Bell',
  email: 'ben@example.com',
  closedAt: '2026-03-05T10:00:00Z',
}
const erased = {
  ...sampleMember,
  id: '4',
  displayName: 'Former member',
  email: 'Former member',
  closedAt: '2026-03-06T10:00:00Z',
  isErased: true,
}

describe('MembersAdminPage', () => {
  it('lists open and closed members, offering only the action that applies', async () => {
    server.use(membersHandler([sampleMember, admin, closed, erased]))

    renderWithProviders(<MembersAdminPage />)

    const open = await screen.findByRole('group', { name: 'Ada Lovelace' })
    expect(within(open).getByText('Active')).toBeInTheDocument()
    expect(within(open).getByRole('button', { name: 'Offboard Ada Lovelace' })).toBeInTheDocument()
    expect(within(open).queryByRole('button', { name: /Erase/ })).not.toBeInTheDocument()

    const self = screen.getByRole('group', { name: 'Rosa Root' })
    expect(within(self).queryByRole('button')).not.toBeInTheDocument()

    const gone = screen.getByRole('group', { name: 'Ben Bell' })
    expect(within(gone).getByText(/^Closed Mar 5, 2026/)).toBeInTheDocument()
    expect(within(gone).queryByRole('button', { name: /Offboard/ })).not.toBeInTheDocument()
    expect(within(gone).getByRole('button', { name: /Erase/ })).toBeInTheDocument()

    const wiped = screen.getByRole('group', { name: 'Former member' })
    expect(within(wiped).getByText(/Personal data erased/)).toBeInTheDocument()
    expect(within(wiped).queryByRole('button')).not.toBeInTheDocument()
  })

  it('says so when there are no members', async () => {
    server.use(membersHandler([]))

    renderWithProviders(<MembersAdminPage />)

    expect(await screen.findByText('There are no members yet.')).toBeInTheDocument()
  })

  it('says so when the list fails to load', async () => {
    server.use(membersFailureHandler())

    renderWithProviders(<MembersAdminPage />)

    expect(await screen.findByText("We couldn't load the members")).toBeInTheDocument()
  })

  it('offboards only after a confirmation that says what happens, then shows the counts', async () => {
    const user = userEvent.setup()
    const offboarded: string[] = []
    server.use(
      membersHandler([sampleMember]),
      offboardHandler((id) => offboarded.push(id), {
        cancelled: 2,
        withdrawn: 1,
        settled: 0,
        burnedQuarterCredits: 42,
      }),
    )

    renderWithProviders(<MembersAdminPage />)
    await user.click(await screen.findByRole('button', { name: 'Offboard Ada Lovelace' }))

    const confirmation = screen.getByRole('alertdialog')
    expect(confirmation).toHaveTextContent(/cancelled/)
    expect(confirmation).toHaveTextContent(/burned/)
    expect(offboarded).toEqual([])

    await user.click(within(confirmation).getByRole('button', { name: 'Offboard member' }))

    expect(await screen.findByRole('status')).toHaveTextContent(
      'Offboarded. 2 Requests cancelled, 1 claim withdrawn, 0 Requests settled, 10.50 credits burned.',
    )
    expect(offboarded).toEqual([sampleMember.id])
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
  })

  it('does nothing when the offboard confirmation is cancelled', async () => {
    const user = userEvent.setup()
    const offboarded: string[] = []
    server.use(
      membersHandler([sampleMember]),
      offboardHandler((id) => offboarded.push(id), {
        cancelled: 0,
        withdrawn: 0,
        settled: 0,
        burnedQuarterCredits: 0,
      }),
    )

    renderWithProviders(<MembersAdminPage />)
    await user.click(await screen.findByRole('button', { name: 'Offboard Ada Lovelace' }))
    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(offboarded).toEqual([])
  })

  it('explains why an offboarding was refused', async () => {
    const user = userEvent.setup()
    server.use(membersHandler([sampleMember]), offboardRejectedHandler(404, 'member_not_found'))

    renderWithProviders(<MembersAdminPage />)
    await user.click(await screen.findByRole('button', { name: 'Offboard Ada Lovelace' }))
    await user.click(screen.getByRole('button', { name: 'Offboard member' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('That member no longer exists.')
  })

  it('erases a closed member only after an irreversible-action confirmation', async () => {
    const user = userEvent.setup()
    const wiped: string[] = []
    server.use(
      membersHandler([closed]),
      eraseHandler((id) => wiped.push(id)),
    )

    renderWithProviders(<MembersAdminPage />)
    await user.click(await screen.findByRole('button', { name: /Erase the personal data of Ben/ }))

    const confirmation = screen.getByRole('alertdialog')
    expect(confirmation).toHaveTextContent("This can't be undone.")
    expect(wiped).toEqual([])

    await user.click(within(confirmation).getByRole('button', { name: 'Erase personal data' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Personal data erased.')
    expect(wiped).toEqual([closed.id])
  })

  it('explains why an erasure was refused', async () => {
    const user = userEvent.setup()
    server.use(membersHandler([closed]), eraseRejectedHandler(422, 'member_not_closed'))

    renderWithProviders(<MembersAdminPage />)
    await user.click(await screen.findByRole('button', { name: /Erase the personal data of Ben/ }))
    await user.click(screen.getByRole('button', { name: 'Erase personal data' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      "Only a closed member's personal data can be erased.",
    )
  })
})
