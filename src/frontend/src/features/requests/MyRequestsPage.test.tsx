import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { MyRequestsPage } from './MyRequestsPage'
import {
  editNotesHandler,
  editNotesRejectedHandler,
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

    const [posted, other] = await screen.findAllByRole('listitem')
    if (!posted || !other) throw new Error('expected two Requests')
    expect(posted).toHaveTextContent(/riverside/i)
    expect(within(posted).getByText('Posted')).toBeInTheDocument()
    expect(within(posted).getByText('2.00')).toBeInTheDocument()
    expect(within(posted).getByText('12 River Rd')).toBeInTheDocument()
    expect(within(posted).getByText(/Mar 2, 2026, 10:00 AM UTC/)).toBeInTheDocument()
    expect(within(other).getByText('Expired')).toBeInTheDocument()
  })

  it('shows the sitter’s house instead of an area for a Request without a location', async () => {
    server.use(
      myRequestsHandler({
        items: [{ ...sampleRequest, area: null, address: null }],
        nextCursor: null,
      }),
    )

    renderWithProviders(<MyRequestsPage />)

    const card = await screen.findByRole('listitem')
    expect(card).toHaveTextContent(/sitter's house/i)
    expect(card).not.toHaveTextContent(/river/i)
  })

  it('names the Sitter on a Claimed Request', async () => {
    server.use(
      myRequestsHandler({
        items: [{ ...sampleRequest, status: 'Claimed', sitterDisplayName: 'Alice Martin' }],
        nextCursor: null,
      }),
    )

    renderWithProviders(<MyRequestsPage />)

    expect(await screen.findByText('Claimed by Alice Martin')).toBeInTheDocument()
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

  it('shows the notes on Posted and Claimed cards', async () => {
    server.use(
      myRequestsHandler({
        items: [
          { ...sampleRequest, notes: 'the youngest has a cold' },
          { ...sampleRequest, id: 'b', status: 'Claimed', notes: 'bedtime at 8' },
        ],
        nextCursor: null,
      }),
    )

    renderWithProviders(<MyRequestsPage />)

    expect(await screen.findByText('the youngest has a cold')).toBeInTheDocument()
    expect(screen.getByText('bedtime at 8')).toBeInTheDocument()
  })

  it('saves edited notes and says the claim is unaffected', async () => {
    const user = userEvent.setup()
    const edits: [string, string | null][] = []
    server.use(
      myRequestsHandler({
        items: [{ ...sampleRequest, status: 'Claimed', notes: 'old' }],
        nextCursor: null,
      }),
      editNotesHandler((id, notes) => edits.push([id, notes])),
    )

    renderWithProviders(<MyRequestsPage />)
    await user.click(await screen.findByRole('button', { name: /edit notes/i }))
    expect(screen.getByText(/doesn.t affect the claim/i)).toBeInTheDocument()
    const field = screen.getByLabelText(/notes for the sitter/i)
    await user.clear(field)
    await user.type(field, 'new')
    expect(screen.getByText('3 of 500 characters')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /save notes/i }))

    await vi.waitFor(() => {
      expect(edits).toEqual([[sampleRequest.id, 'new']])
    })
  })

  it('clears the notes by saving them empty', async () => {
    const user = userEvent.setup()
    const edits: (string | null)[] = []
    server.use(
      myRequestsHandler({ items: [{ ...sampleRequest, notes: 'old' }], nextCursor: null }),
      editNotesHandler((_, notes) => edits.push(notes)),
    )

    renderWithProviders(<MyRequestsPage />)
    await user.click(await screen.findByRole('button', { name: /edit notes/i }))
    await user.clear(screen.getByLabelText(/notes for the sitter/i))
    await user.click(screen.getByRole('button', { name: /save notes/i }))

    await vi.waitFor(() => {
      expect(edits).toEqual([''])
    })
  })

  it.each([
    ['start_passed', 422, /start time has passed/i],
    ['invalid_state', 409, /no longer available/i],
  ])('explains a rejected notes edit (%s)', async (code, status, message) => {
    const user = userEvent.setup()
    server.use(
      myRequestsHandler({ items: [sampleRequest], nextCursor: null }),
      editNotesRejectedHandler(status, code),
    )

    renderWithProviders(<MyRequestsPage />)
    await user.click(await screen.findByRole('button', { name: /add notes/i }))
    await user.type(screen.getByLabelText(/notes for the sitter/i), 'x')
    await user.click(screen.getByRole('button', { name: /save notes/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(message)
  })
})
