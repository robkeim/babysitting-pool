import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { PoolAdminPage } from './PoolAdminPage'
import {
  dormantHandler,
  failureHandler,
  poolOverviewHandler,
  sampleDormant,
  sampleOverview,
  settleHandler,
  settleRejectedHandler,
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

  describe('Run settle', () => {
    beforeEach(() => {
      server.use(poolOverviewHandler(sampleOverview), dormantHandler([]))
    })

    it('asks for confirmation, runs Settle and reports what it did', async () => {
      const onSettle = vi.fn()
      server.use(settleHandler(onSettle, { expired: 1, completed: 2, charged: 3, failed: 0 }))
      const user = userEvent.setup()
      renderWithProviders(<PoolAdminPage />)

      await user.click(await screen.findByRole('button', { name: 'Run settle' }))
      expect(onSettle).not.toHaveBeenCalled()
      await user.click(
        within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Run settle' }),
      )

      expect(
        await screen.findByText(
          'Settle ran. 2 Requests paid out, 1 Request expired and refunded, 3 Members charged Demurrage.',
        ),
      ).toBeVisible()
      expect(onSettle).toHaveBeenCalledOnce()
      expect(screen.queryByText(/will be retried/)).not.toBeInTheDocument()
    })

    it('runs nothing when the confirmation is canceled', async () => {
      const onSettle = vi.fn()
      server.use(settleHandler(onSettle, { expired: 0, completed: 0, charged: 0, failed: 0 }))
      const user = userEvent.setup()
      renderWithProviders(<PoolAdminPage />)

      await user.click(await screen.findByRole('button', { name: 'Run settle' }))
      await user.click(
        within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Cancel' }),
      )

      await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument())
      expect(onSettle).not.toHaveBeenCalled()
    })

    it('says how many items failed and will be retried', async () => {
      server.use(settleHandler(vi.fn(), { expired: 0, completed: 1, charged: 0, failed: 2 }))
      const user = userEvent.setup()
      renderWithProviders(<PoolAdminPage />)

      await user.click(await screen.findByRole('button', { name: 'Run settle' }))
      await user.click(
        within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Run settle' }),
      )

      expect(
        await screen.findByText('2 items failed and will be retried on the next run.'),
      ).toBeVisible()
    })

    it('shows an error when the run is rejected', async () => {
      server.use(settleRejectedHandler(500, 'internal_error'))
      const user = userEvent.setup()
      renderWithProviders(<PoolAdminPage />)

      await user.click(await screen.findByRole('button', { name: 'Run settle' }))
      await user.click(
        within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Run settle' }),
      )

      expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong.')
    })
  })
})
