import { screen, within } from '@testing-library/react'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { MembersAdminPage } from './MembersAdminPage'
import { failureHandler, membersHandler, sampleMember } from './mocks'

// The pages sit inside the router in the app; here a link only needs to render as one.
vi.mock('@tanstack/react-router', () => ({
  Link: ({
    children,
    params,
    ...rest
  }: {
    children: React.ReactNode
    params?: { memberId: string }
  }) => (
    <a href={`/admin/members/${params?.memberId ?? ''}`} {...rest}>
      {children}
    </a>
  ),
}))

describe('MembersAdminPage', () => {
  it('lists each member with balances, marking closed ones and the Admin', async () => {
    server.use(
      membersHandler([
        sampleMember,
        { ...sampleMember, id: '2', displayName: 'Ben', closedAt: '2026-02-01T09:00:00Z' },
        { ...sampleMember, id: '3', displayName: 'Cleo', isAdmin: true },
      ]),
    )

    renderWithProviders(<MembersAdminPage />)

    const ada = await screen.findByRole('group', { name: 'Ada Lovelace' })
    expect(within(ada).getByText('10.25 available · 2.00 in escrow')).toBeInTheDocument()
    expect(within(ada).getByRole('link', { name: 'Ada Lovelace' })).toHaveAttribute(
      'href',
      `/admin/members/${sampleMember.id}`,
    )
    expect(within(screen.getByRole('group', { name: 'Ben' })).getByText('(Closed)')).toBeVisible()
    expect(within(screen.getByRole('group', { name: 'Cleo' })).getByText('(Admin)')).toBeVisible()
  })

  it('says so when there are no members', async () => {
    server.use(membersHandler([]))

    renderWithProviders(<MembersAdminPage />)

    expect(await screen.findByText('There are no members yet.')).toBeInTheDocument()
  })

  it('says so when the list fails to load', async () => {
    server.use(failureHandler('*/api/admin/members'))

    renderWithProviders(<MembersAdminPage />)

    expect(await screen.findByText("We couldn't load the members")).toBeInTheDocument()
  })
})
