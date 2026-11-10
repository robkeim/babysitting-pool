import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { OpenRequestsPage } from './OpenRequestsPage'
import {
  claimRequestHandler,
  claimRequestRejectedHandler,
  openRequestsFailureHandler,
  openRequestsHandler,
  sampleOpenRequest,
} from './mocks'

describe('OpenRequestsPage', () => {
  it('lists open Requests with the rough area, details and credits, and no address or instructions', async () => {
    server.use(
      openRequestsHandler({
        items: [sampleOpenRequest, { ...sampleOpenRequest, id: 'b', area: 'Hilltop' }],
        nextCursor: null,
      }),
    )

    renderWithProviders(<OpenRequestsPage />)

    const [row, other] = await screen.findAllByRole('listitem')
    if (!row || !other) throw new Error('expected two Requests')
    expect(row).toHaveTextContent(/riverside/i)
    expect(within(row).getByText(/Mar 2, 2026, 10:00 AM UTC/)).toBeInTheDocument()
    expect(within(row).getByText('120 minutes, 2 children')).toBeInTheDocument()
    expect(within(row).getByText('3.00')).toBeInTheDocument()
    expect(other).toHaveTextContent(/hilltop/i)
    expect(row).not.toHaveTextContent(/ring twice/i)
  })

  it('says so when nothing is open', async () => {
    server.use(openRequestsHandler({ items: [], nextCursor: null }))

    renderWithProviders(<OpenRequestsPage />)

    expect(await screen.findByText(/no open requests/i)).toBeInTheDocument()
  })

  it('claims the chosen Request', async () => {
    const user = userEvent.setup()
    const claimed: string[] = []
    server.use(
      openRequestsHandler({ items: [sampleOpenRequest], nextCursor: null }),
      claimRequestHandler((id) => claimed.push(id)),
    )

    renderWithProviders(<OpenRequestsPage />)
    await user.click(await screen.findByRole('button', { name: /claim the request in riverside/i }))

    expect(claimed).toEqual([sampleOpenRequest.id])
  })

  it.each([
    [409, 'invalid_state', /no longer available/i],
    [422, 'claim_cutoff_passed', /too late to claim/i],
    [422, 'own_request', /can.t claim your own/i],
  ])('explains a rejected claim (%s %s)', async (status, code, message) => {
    const user = userEvent.setup()
    server.use(
      openRequestsHandler({ items: [sampleOpenRequest], nextCursor: null }),
      claimRequestRejectedHandler(status, code),
    )

    renderWithProviders(<OpenRequestsPage />)
    await user.click(await screen.findByRole('button', { name: /claim the request in riverside/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(message)
  })

  it('shows an error without exposing the server message', async () => {
    server.use(openRequestsFailureHandler())

    renderWithProviders(<OpenRequestsPage />)

    expect(await screen.findByRole('alert')).toHaveTextContent(/couldn.t load the open requests/i)
    expect(screen.queryByText('Boom')).not.toBeInTheDocument()
  })

  it('shows a reference a Member can quote when a load fails', async () => {
    server.use(openRequestsFailureHandler())

    renderWithProviders(<OpenRequestsPage />)

    expect(await screen.findByText('f'.repeat(32))).toBeInTheDocument()
  })
})
