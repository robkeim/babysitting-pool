import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { MyClaimsPage } from './MyClaimsPage'
import {
  myClaimsFailureHandler,
  myClaimsHandler,
  releaseRequestHandler,
  releaseRequestRejectedHandler,
  sampleRequest,
} from './mocks'

describe('MyClaimsPage', () => {
  it('lists the Member’s claims with state, credits, area, the exact address and its instructions', async () => {
    server.use(
      myClaimsHandler({
        items: [
          { ...sampleRequest, status: 'Claimed' },
          { ...sampleRequest, id: 'b', status: 'Completed', area: 'Hilltop' },
        ],
        nextCursor: null,
      }),
    )

    renderWithProviders(<MyClaimsPage />)

    const [claimed, other] = await screen.findAllByRole('listitem')
    if (!claimed || !other) throw new Error('expected two Requests')
    expect(claimed).toHaveTextContent(/riverside/i)
    expect(within(claimed).getByText('Claimed')).toBeInTheDocument()
    expect(within(claimed).getByText('2.00')).toBeInTheDocument()
    expect(within(claimed).getByText('12 River Rd')).toBeInTheDocument()
    expect(within(claimed).getByText('ring twice')).toBeInTheDocument()
    expect(within(other).getByText('Completed')).toBeInTheDocument()
  })

  it('says so when nothing has been claimed', async () => {
    server.use(myClaimsHandler({ items: [], nextCursor: null }))

    renderWithProviders(<MyClaimsPage />)

    expect(await screen.findByText(/haven.t claimed any requests/i)).toBeInTheDocument()
  })

  it('shows an error without exposing the server message', async () => {
    server.use(myClaimsFailureHandler())

    renderWithProviders(<MyClaimsPage />)

    expect(await screen.findByRole('alert')).toHaveTextContent(/couldn.t load your claims/i)
    expect(screen.queryByText('Boom')).not.toBeInTheDocument()
  })

  it('withdraws from a Claimed Request, and offers nothing for completed ones', async () => {
    const user = userEvent.setup()
    const withdrawn: string[] = []
    server.use(
      myClaimsHandler({
        items: [
          { ...sampleRequest, status: 'Claimed' },
          { ...sampleRequest, id: 'b', status: 'Completed', area: 'Hilltop' },
        ],
        nextCursor: null,
      }),
      releaseRequestHandler('withdraw', 'Posted', (id) => withdrawn.push(id)),
    )

    renderWithProviders(<MyClaimsPage />)
    await user.click(
      await screen.findByRole('button', { name: /withdraw from the request in riverside/i }),
    )

    await vi.waitFor(() => {
      expect(withdrawn).toEqual([sampleRequest.id])
    })
    expect(screen.getAllByRole('button', { name: /withdraw from/i })).toHaveLength(1)
  })

  it('explains a rejected withdraw', async () => {
    const user = userEvent.setup()
    server.use(
      myClaimsHandler({ items: [{ ...sampleRequest, status: 'Claimed' }], nextCursor: null }),
      releaseRequestRejectedHandler('withdraw', 404, 'request_not_found'),
    )

    renderWithProviders(<MyClaimsPage />)
    await user.click(
      await screen.findByRole('button', { name: /withdraw from the request in riverside/i }),
    )

    expect(await screen.findByRole('alert')).toHaveTextContent(/no longer exists/i)
  })
})
