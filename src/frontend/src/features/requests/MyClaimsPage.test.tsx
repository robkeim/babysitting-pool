import { screen, within } from '@testing-library/react'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { MyClaimsPage } from './MyClaimsPage'
import { myClaimsFailureHandler, myClaimsHandler, sampleRequest } from './mocks'

describe('MyClaimsPage', () => {
  it('lists the Member’s claims with state, credits, area and the exact address', async () => {
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

    const claimed = await screen.findByRole('row', { name: /riverside/i })
    expect(within(claimed).getByText('Claimed')).toBeInTheDocument()
    expect(within(claimed).getByText('2.00')).toBeInTheDocument()
    expect(within(claimed).getByText('12 River Rd')).toBeInTheDocument()
    expect(
      within(screen.getByRole('row', { name: /hilltop/i })).getByText('Completed'),
    ).toBeInTheDocument()
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
})
