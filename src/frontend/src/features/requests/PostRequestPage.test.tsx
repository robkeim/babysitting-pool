import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { http, HttpResponse } from 'msw'
import { PostRequestPage } from './PostRequestPage'
import {
  availableBalanceHandler,
  postRequestHandler,
  postRequestRejectedHandler,
  profileHandler,
  quoteHandler,
  sampleProfile,
} from './mocks'
import type { PostRequestBody } from './types'

async function fillRequiredFields(user: ReturnType<typeof userEvent.setup>) {
  const startsAt = screen.getByLabelText(/start time/i)
  await user.clear(startsAt)
  await user.type(startsAt, '2026-03-02T10:00')
}

beforeEach(() => {
  server.use(profileHandler(sampleProfile))
})

describe('PostRequestPage', () => {
  it('defaults the start time to the next 5-minute mark', () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 2, 2, 14, 12, 30))
    try {
      renderWithProviders(<PostRequestPage onPosted={vi.fn()} />)
      expect(screen.getByLabelText(/start time/i)).toHaveValue('2026-03-02T14:15')
    } finally {
      vi.useRealTimers()
    }
  })

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
    })
    expect(posted[0]).not.toHaveProperty('area')
    expect(posted[0]).not.toHaveProperty('address')
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
    expect(screen.getByLabelText('Transport')).toHaveAccessibleDescription(
      /not available when the babysitting is at the requesting family/i,
    )
  })

  it('asks for no area or address, and shows the profile’s only at the requesting family’s house', async () => {
    const user = userEvent.setup()
    server.use(availableBalanceHandler(40), quoteHandler({ costQuarterCredits: 6 }))

    renderWithProviders(<PostRequestPage onPosted={vi.fn()} />)

    expect(screen.queryByLabelText(/area/i)).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/address/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/12 River Rd/)).not.toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Location'), 'RequestingFamilysHouse')

    expect(await screen.findByText(/Riverside, 12 River Rd/)).toBeInTheDocument()
    expect(screen.queryByLabelText(/area/i)).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/address/i)).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: /change them in your profile/i })).toHaveAttribute(
      'href',
      '/profile',
    )
    expect(screen.getByRole('button', { name: /post request/i })).toBeEnabled()
  })

  it('blocks a request at the requesting family’s house until the profile has an address', async () => {
    const user = userEvent.setup()
    server.use(
      availableBalanceHandler(40),
      quoteHandler({ costQuarterCredits: 6 }),
      profileHandler({ area: null, address: null }),
    )

    renderWithProviders(<PostRequestPage onPosted={vi.fn()} />)
    await user.selectOptions(screen.getByLabelText('Location'), 'RequestingFamilysHouse')

    expect(await screen.findByRole('alert')).toHaveTextContent(/add your address to your profile/i)
    expect(screen.getByRole('link', { name: /open your profile/i })).toHaveAttribute(
      'href',
      '/profile',
    )
    expect(screen.getByRole('button', { name: /post request/i })).toBeDisabled()

    await user.selectOptions(screen.getByLabelText('Location'), 'SittersHouse')

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /post request/i })).toBeEnabled()
  })

  it('explains a start time that is inside the minimum lead time', async () => {
    const user = userEvent.setup()
    server.use(
      availableBalanceHandler(40),
      quoteHandler({ costQuarterCredits: 8 }),
      postRequestRejectedHandler('below_minimum_lead_time'),
      http.get('*/api/rules', () => HttpResponse.json({ minimumLeadTimeMinutes: 360 })),
    )

    renderWithProviders(<PostRequestPage onPosted={vi.fn()} />)
    await fillRequiredFields(user)
    await user.click(screen.getByRole('button', { name: /post request/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/at least 6 hours/i)
  })

  it('prices each option inline and itemises the backend quote as a receipt', async () => {
    const user = userEvent.setup()
    server.use(
      availableBalanceHandler(84),
      quoteHandler({
        costQuarterCredits: 16,
        lines: {
          hourlyRateQuarterCredits: 7,
          timeQuarterCredits: 7,
          locationQuarterCredits: 0,
          transportQuarterCredits: 0,
          lunchQuarterCredits: 6,
          snackQuarterCredits: 3,
          dinnerQuarterCredits: 0,
        },
        options: {
          familyHouseQuarterCredits: 2,
          oneWayTransportQuarterCredits: 2,
          roundTripTransportQuarterCredits: 3,
          lunchQuarterCredits: 6,
          snackQuarterCredits: 3,
          dinnerQuarterCredits: 6,
          lunchPerChildQuarterCredits: 2,
          snackPerChildQuarterCredits: 1,
          dinnerPerChildQuarterCredits: 2,
          secondChildHourlyQuarterCredits: 2,
          additionalChildHourlyQuarterCredits: 1,
        },
      }),
    )

    renderWithProviders(<PostRequestPage onPosted={vi.fn()} />)
    await user.clear(screen.getByLabelText(/number of children/i))
    await user.type(screen.getByLabelText(/number of children/i), '3')

    expect(await screen.findByLabelText(/lunch \(\+1\.50 credits\)/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/snack \(\+0\.75 credits\)/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/dinner \(\+1\.50 credits\)/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/lunch/i)).toHaveAccessibleName(/0\.50 × 3 children/)
    expect(
      screen.getByRole('option', { name: /requesting family's house \(\+0\.50 credits\)/i }),
    ).toBeInTheDocument()
    expect(screen.getByRole('option', { name: /one way \(\+0\.50 credits\)/i })).toBeInTheDocument()
    expect(
      screen.getByRole('option', { name: /round trip \(\+0\.75 credits\)/i }),
    ).toBeInTheDocument()
    expect(screen.getByText(/1\.75 credits per hour for 3 children/i)).toBeInTheDocument()

    const receipt = screen.getByRole('region', { name: /cost breakdown/i })
    await user.click(within(receipt).getByRole('button', { name: /show breakdown/i }))
    expect(within(receipt).getByText(/time \(60 min at 1\.75\/hr\)/i)).toBeInTheDocument()
    expect(within(receipt).getByText('1.75')).toBeInTheDocument()
    expect(within(receipt).getByText('1.50')).toBeInTheDocument()
    expect(within(receipt).getByText('0.75')).toBeInTheDocument()
    expect(within(receipt).queryByText(/dinner/i)).not.toBeInTheDocument()
    expect(within(receipt).getByText('4.00 credits')).toBeInTheDocument()
    expect(within(receipt).getByText(/you have 21\.00 available/i)).toBeInTheDocument()
    expect(within(receipt).getByText(/after posting: 17\.00 credits left/i)).toBeInTheDocument()
    expect(within(receipt).getByRole('link', { name: /how is this calculated/i })).toHaveAttribute(
      'href',
      '/how-it-works#pricing',
    )
  })

  it('keeps the form compact: breakdown collapsed, per-child detail only for several children', async () => {
    const user = userEvent.setup()
    renderWithProviders(<PostRequestPage onPosted={vi.fn()} />)

    const receipt = await screen.findByRole('region', { name: /cost breakdown/i })
    const toggle = within(receipt).getByRole('button', { name: /show breakdown/i })
    expect(toggle).toHaveAttribute('aria-expanded', 'false')
    const details = document.getElementById(toggle.getAttribute('aria-controls') ?? '')
    expect(details).not.toBeVisible()
    expect(screen.queryByText(/credits per hour for/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/× 1 child/)).not.toBeInTheDocument()

    await user.click(toggle)
    expect(within(receipt).getByRole('button', { name: /hide breakdown/i })).toHaveAttribute(
      'aria-expanded',
      'true',
    )
    expect(details).toBeVisible()
  })

  it('sends the notes with a counter, and null when left blank', async () => {
    const user = userEvent.setup()
    const posted: PostRequestBody[] = []
    server.use(
      availableBalanceHandler(40),
      quoteHandler({ costQuarterCredits: 8 }),
      postRequestHandler((body) => posted.push(body)),
    )

    renderWithProviders(<PostRequestPage onPosted={vi.fn()} />)
    await fillRequiredFields(user)
    expect(screen.getByText('0 of 500 characters')).toBeInTheDocument()
    await user.type(screen.getByLabelText(/notes for the sitter/i), 'has a cold')
    expect(screen.getByText('10 of 500 characters')).toBeInTheDocument()
    expect(await screen.findByText('2.00 credits')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /post request/i }))

    await vi.waitFor(() => {
      expect(posted[0]).toMatchObject({ notes: 'has a cold' })
    })
  })

  it('shows invalid_notes without exposing the server message', async () => {
    const user = userEvent.setup()
    server.use(
      availableBalanceHandler(40),
      quoteHandler({ costQuarterCredits: 8 }),
      postRequestRejectedHandler('invalid_notes'),
    )

    renderWithProviders(<PostRequestPage onPosted={vi.fn()} />)
    await fillRequiredFields(user)
    expect(await screen.findByText('2.00 credits')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /post request/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/notes are too long/i)
  })
})
