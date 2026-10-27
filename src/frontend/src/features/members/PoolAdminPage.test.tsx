import { screen, within } from '@testing-library/react'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { PoolAdminPage } from './PoolAdminPage'
import {
  dormantHandler,
  failureHandler,
  poolOverviewHandler,
  sampleDormant,
  sampleOverview,
} from './mocks'

vi.mock('@tanstack/react-router', () => ({
  Link: ({ children, id }: { children: React.ReactNode; id?: string }) => (
    <a id={id} href="/member">
      {children}
    </a>
  ),
}))

describe('PoolAdminPage', () => {
  it('shows the pool totals with both kinds of burn and the requests by state', async () => {
    server.use(poolOverviewHandler(sampleOverview), dormantHandler([]))

    renderWithProviders(<PoolAdminPage />)

    const figure = async (name: string) => within(await screen.findByRole('group', { name }))
    expect((await figure('Credits in circulation')).getByText('100.00')).toBeVisible()
    expect((await figure('In escrow')).getByText('6.00')).toBeVisible()
    expect((await figure('Burned by Demurrage')).getByText('4.00')).toBeVisible()
    expect((await figure('Burned at Offboarding')).getByText('10.00')).toBeVisible()
    expect((await figure('Open members')).getByText('7')).toBeVisible()
    expect((await figure('Completed')).getByText('11')).toBeVisible()
  })

  it('lists dormant members with last activity and balance, or says there are none', async () => {
    server.use(poolOverviewHandler(sampleOverview), dormantHandler([sampleDormant]))

    const { unmount } = renderWithProviders(<PoolAdminPage />)

    const row = await screen.findByRole('group', { name: 'Ada Lovelace' })
    expect(within(row).getByText(/Last activity Jan 5, 2026/)).toBeInTheDocument()
    expect(within(row).getByText('10.25 available')).toBeInTheDocument()
    unmount()

    server.use(dormantHandler([]))
    renderWithProviders(<PoolAdminPage />)
    expect(await screen.findByText('No members are dormant.')).toBeInTheDocument()
  })

  it('says so when either view fails to load', async () => {
    server.use(
      failureHandler('*/api/admin/pool-overview'),
      failureHandler('*/api/admin/dormant-members'),
    )

    renderWithProviders(<PoolAdminPage />)

    expect(await screen.findByText("We couldn't load the pool overview")).toBeInTheDocument()
    expect(await screen.findByText("We couldn't load the dormant members")).toBeInTheDocument()
  })
})
