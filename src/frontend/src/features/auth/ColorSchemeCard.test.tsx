import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { ColorSchemeCard } from './ColorSchemeCard'
import { meHandler, signedOutHandler } from './mocks'

const ada = {
  memberId: '2f6c1c3e-5d8a-4a38-9b1d-0a7a1c1b9e11',
  email: 'ada@example.com',
  displayName: 'Ada',
  language: 'en-US',
  colorScheme: 'system',
  isAdmin: false,
}

describe('ColorSchemeCard', () => {
  it('offers light, dark and the device setting, with the Member’s choice selected', async () => {
    server.use(meHandler({ ...ada, colorScheme: 'dark' }))
    renderWithProviders(<ColorSchemeCard />)

    const dropdown = await screen.findByLabelText('Appearance')

    expect(dropdown).toHaveValue('dark')
    expect(screen.getAllByRole('option').map((option) => option.textContent)).toEqual([
      'Match my device',
      'Light',
      'Dark',
    ])
  })

  it('saves the Color Scheme the Member picks and shows it selected', async () => {
    let saved: unknown
    server.use(
      meHandler(ada),
      http.put('*/api/me/color-scheme', async ({ request }) => {
        saved = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderWithProviders(<ColorSchemeCard />)

    const dropdown = await screen.findByLabelText('Appearance')
    await userEvent.selectOptions(dropdown, 'Light')

    await waitFor(() => {
      expect(dropdown).toHaveValue('light')
    })
    expect(saved).toEqual({ colorScheme: 'light' })
  })

  it('renders nothing when nobody is signed in', async () => {
    server.use(signedOutHandler())

    const { container } = renderWithProviders(<ColorSchemeCard />)

    await waitFor(() => {
      expect(container).toBeEmptyDOMElement()
    })
  })
})
