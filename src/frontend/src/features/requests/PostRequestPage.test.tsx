import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { PostRequestPage } from './PostRequestPage'
import {
  availableBalanceHandler,
  postRequestHandler,
  postRequestRejectedHandler,
  quoteHandler,
} from './mocks'
import type { PostRequestBody } from './types'

async function fillRequiredFields(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText(/start time/i), '2026-03-02T10:00')
  await user.type(screen.getByLabelText(/^area/i), 'Riverside')
  await user.type(screen.getByLabelText(/exact address/i), '12 River Rd')
}

describe('PostRequestPage', () => {
  it('previews the cost from the calculator and posts the details', async () => {
    const user = userEvent.setup()
    const posted: PostRequestBody[] = []
    const onPosted = vi.fn()
    server.use(
      availableBalanceHandler(40),
      quoteHandler({ costQuarterCredits: 8 }),
      postRequestHandler((body) => posted.push(body)),
    )

    renderWithProviders(<PostRequestPage onPosted={onPosted} />)
    await fillRequiredFields(user)

    expect(await screen.findByText('2.00 credits')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /post request/i }))

    expect(onPosted).toHaveBeenCalled()
    expect(posted).toHaveLength(1)
    expect(posted[0]).toMatchObject({
      durationMinutes: 60,
      childCount: 1,
      location: 'SittersHouse',
      transport: 'None',
      lunch: false,
      snack: false,
      dinner: false,
      area: 'Riverside',
      address: '12 River Rd',
    })
    expect(new Date(posted[0]?.startsAt ?? '').getTime()).toBe(
      new Date('2026-03-02T10:00').getTime(),
    )
  })

  it('blocks posting when the cost is more than the available balance', async () => {
    const user = userEvent.setup()
    server.use(availableBalanceHandler(4), quoteHandler({ costQuarterCredits: 8 }))

    renderWithProviders(<PostRequestPage onPosted={vi.fn()} />)
    await fillRequiredFields(user)

    expect(await screen.findByRole('alert')).toHaveTextContent(/not enough credits/i)
    expect(screen.getByRole('button', { name: /post request/i })).toBeDisabled()
  })

  it('blocks posting with no available credits at all', async () => {
    const user = userEvent.setup()
    server.use(availableBalanceHandler(0), quoteHandler({ costQuarterCredits: 4 }))

    renderWithProviders(<PostRequestPage onPosted={vi.fn()} />)
    await fillRequiredFields(user)

    expect(await screen.findByRole('alert')).toHaveTextContent(/not enough credits/i)
    expect(screen.getByRole('button', { name: /post request/i })).toBeDisabled()
  })

  it('rejects a duration that is not a multiple of 15 minutes', async () => {
    const user = userEvent.setup()
    server.use(availableBalanceHandler(40), quoteHandler({ costQuarterCredits: 8 }))

    renderWithProviders(<PostRequestPage onPosted={vi.fn()} />)
    await fillRequiredFields(user)
    const duration = screen.getByLabelText(/duration/i)
    await user.clear(duration)
    await user.type(duration, '32')

    expect(await screen.findByRole('alert')).toHaveTextContent(/steps of 15 minutes/i)
    expect(duration).toBeInvalid()
    expect(screen.getByRole('button', { name: /post request/i })).toBeDisabled()

    await user.clear(duration)
    await user.type(duration, '30')

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /post request/i })).toBeEnabled()
  })

  it('does not offer transport at the requesting family’s house', async () => {
    const user = userEvent.setup()
    server.use(availableBalanceHandler(40), quoteHandler({ costQuarterCredits: 6 }))

    renderWithProviders(<PostRequestPage onPosted={vi.fn()} />)
    await user.selectOptions(screen.getByLabelText('Transport'), 'OneWay')
    await user.selectOptions(screen.getByLabelText('Location'), 'RequestingFamilysHouse')

    expect(screen.getByLabelText('Transport')).toBeDisabled()
    expect(screen.getByLabelText('Transport')).toHaveValue('None')
  })

  it('explains a start time that is inside the minimum lead time', async () => {
    const user = userEvent.setup()
    server.use(
      availableBalanceHandler(40),
      quoteHandler({ costQuarterCredits: 8 }),
      postRequestRejectedHandler('below_minimum_lead_time'),
    )

    renderWithProviders(<PostRequestPage onPosted={vi.fn()} />)
    await fillRequiredFields(user)
    await user.click(screen.getByRole('button', { name: /post request/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/too soon/i)
  })
})
