import { screen, within } from '@testing-library/react'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { MemberDetailPage } from './MemberDetailPage'
import { failureHandler, memberDetailHandler, memberRequestsHandler, sampleMember } from './mocks'

describe('MemberDetailPage', () => {
  it('shows balances, requests and ledger, with nothing to edit', async () => {
    server.use(
      memberDetailHandler({
        member: sampleMember,
        ledger: {
          items: [
            {
              id: 'l1',
              kind: 'Demurrage',
              amountQuarterCredits: 8,
              occurredAt: '2026-02-02T00:00:00Z',
            },
          ],
          nextCursor: null,
        },
      }),
      memberRequestsHandler([
        {
          id: 'r1',
          role: 'Sitter',
          startsAt: '2026-02-10T18:00:00Z',
          durationMinutes: 120,
          childCount: 2,
          area: 'Riverside',
          costQuarterCredits: 12,
          status: 'Completed',
        },
      ]),
    )

    renderWithProviders(<MemberDetailPage memberId={sampleMember.id} />)

    expect(await screen.findByRole('heading', { name: 'Ada Lovelace' })).toBeInTheDocument()
    expect(
      within(screen.getByRole('group', { name: 'Available' })).getByText('10.25'),
    ).toBeVisible()
    expect(within(screen.getByRole('group', { name: 'In escrow' })).getByText('2.00')).toBeVisible()
    expect(await screen.findByText(/Sitting · Completed · Riverside/)).toBeInTheDocument()
    expect(screen.getByText('Demurrage')).toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('says so when the member cannot be loaded', async () => {
    server.use(failureHandler('*/api/admin/members/:id'), memberRequestsHandler([]))

    renderWithProviders(<MemberDetailPage memberId={sampleMember.id} />)

    expect(await screen.findByText("We couldn't load this member")).toBeInTheDocument()
  })
})
