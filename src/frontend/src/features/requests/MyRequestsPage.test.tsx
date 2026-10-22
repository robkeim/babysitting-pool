import { screen, within } from '@testing-library/react'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { MyRequestsPage } from './MyRequestsPage'
import { myRequestsFailureHandler, myRequestsHandler, sampleRequest } from './mocks'

describe('MyRequestsPage', () => {
  it('lists the Member’s Requests with state, cost, area and their own address', async () => {
    server.use(
      myRequestsHandler({
        items: [sampleRequest, { ...sampleRequest, id: 'b', status: 'Expired', area: 'Hilltop' }],
        nextCursor: null,
      }),
    )

    renderWithProviders(<MyRequestsPage />)

    const posted = await screen.findByRole('row', { name: /riverside/i })
    expect(within(posted).getByText('Posted')).toBeInTheDocument()
    expect(within(posted).getByText('2.00')).toBeInTheDocument()
    expect(within(posted).getByText('12 River Rd')).toBeInTheDocument()
    expect(within(posted).getByText(/2 Mar 2026, 10:00 UTC/)).toBeInTheDocument()
    expect(
      within(screen.getByRole('row', { name: /hilltop/i })).getByText('Expired'),
    ).toBeInTheDocument()
  })

  it('says so when nothing has been posted', async () => {
    server.use(myRequestsHandler({ items: [], nextCursor: null }))

    renderWithProviders(<MyRequestsPage />)

    expect(await screen.findByText(/haven.t posted any requests/i)).toBeInTheDocument()
  })

  it('shows an error without exposing the server message', async () => {
    server.use(myRequestsFailureHandler())

    renderWithProviders(<MyRequestsPage />)

    expect(await screen.findByRole('alert')).toHaveTextContent(/couldn.t load your requests/i)
    expect(screen.queryByText('Boom')).not.toBeInTheDocument()
  })
})
