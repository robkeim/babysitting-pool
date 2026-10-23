import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { MyRequestsPage } from './MyRequestsPage'
import {
  myRequestsFailureHandler,
  myRequestsHandler,
  releaseRequestHandler,
  releaseRequestRejectedHandler,
  sampleRequest,
} from './mocks'

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
    expect(within(posted).getByText(/Mar 2, 2026, 10:00 AM UTC/)).toBeInTheDocument()
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

  it('cancels a Posted or Claimed Request, and offers nothing for ended ones', async () => {
    const user = userEvent.setup()
    const cancelled: string[] = []
    server.use(
      myRequestsHandler({
        items: [
          sampleRequest,
          { ...sampleRequest, id: 'b', status: 'Claimed', area: 'Hilltop' },
          { ...sampleRequest, id: 'c', status: 'Expired', area: 'Lakeside' },
        ],
        nextCursor: null,
      }),
      releaseRequestHandler('cancel', 'Cancelled', (id) => cancelled.push(id)),
    )

    renderWithProviders(<MyRequestsPage />)
    await user.click(await screen.findByRole('button', { name: /cancel the request in hilltop/i }))

    await vi.waitFor(() => {
      expect(cancelled).toEqual(['b'])
    })
    expect(screen.getAllByRole('button', { name: /cancel the request/i })).toHaveLength(2)
  })

  it('explains a rejected cancel without exposing the server message', async () => {
    const user = userEvent.setup()
    server.use(
      myRequestsHandler({ items: [sampleRequest], nextCursor: null }),
      releaseRequestRejectedHandler('cancel', 422, 'start_passed'),
    )

    renderWithProviders(<MyRequestsPage />)
    await user.click(
      await screen.findByRole('button', { name: /cancel the request in riverside/i }),
    )

    expect(await screen.findByRole('alert')).toHaveTextContent(/start time has passed/i)
  })
})
