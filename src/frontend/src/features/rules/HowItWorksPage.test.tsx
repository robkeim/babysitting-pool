import { screen, within } from '@testing-library/react'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { HowItWorksPage } from './HowItWorksPage'
import { rulesFailureHandler, rulesHandler, sampleRules } from './mocks'

describe('HowItWorksPage', () => {
  it('shows the four sections with the numbers the API gives', async () => {
    server.use(rulesHandler())

    renderWithProviders(<HowItWorksPage />)

    const credits = await screen.findByRole('region', { name: 'Your credits' })
    expect(credits).toHaveAttribute('id', 'credits')
    expect(within(credits).getByText(/you get 10\.00 credits/i)).toBeInTheDocument()

    const pricing = screen.getByRole('region', { name: 'What a request costs' })
    expect(pricing).toHaveAttribute('id', 'pricing')
    expect(within(pricing).getByText(/second child adds 0\.50 per hour/i)).toBeInTheDocument()

    const requests = screen.getByRole('region', { name: 'Posting, claiming and canceling' })
    expect(requests).toHaveAttribute('id', 'requests')
    expect(within(requests).getByText(/at least 6 hours after/i)).toBeInTheDocument()
    expect(within(requests).getByText(/until 3 hours before/i)).toBeInTheDocument()

    const demurrage = screen.getByRole('region', { name: 'Staying active' })
    expect(demurrage).toHaveAttribute('id', 'demurrage')
    expect(
      within(demurrage).getByText(
        /more than 21 days, 2\.00 credits .* every Monday at 00:00 \(UTC\)/,
      ),
    ).toBeInTheDocument()
    expect(within(demurrage).getByText(/after 6 weeks/i)).toBeInTheDocument()
  })

  it("shows each worked example's total as the API priced it", async () => {
    server.use(rulesHandler())

    renderWithProviders(<HowItWorksPage />)

    const pricing = await screen.findByRole('region', { name: 'What a request costs' })
    const [example] = sampleRules.examples
    expect(
      within(pricing).getByText(/2 children for 90 minutes, Requesting family's house/),
    ).toBeInTheDocument()
    expect(
      within(pricing).getByText(
        `${((example?.quote.costQuarterCredits ?? 0) / 4).toFixed(2)} credits`,
      ),
    ).toBeInTheDocument()
  })

  it('says so when the rules cannot be loaded', async () => {
    server.use(rulesFailureHandler())

    renderWithProviders(<HowItWorksPage />)

    expect(await screen.findByText("We couldn't load the rules")).toBeInTheDocument()
  })
})
