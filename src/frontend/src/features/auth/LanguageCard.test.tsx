import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { LanguageCard } from './LanguageCard'
import { meHandler, signedOutHandler } from './mocks'

const ada = {
  memberId: '2f6c1c3e-5d8a-4a38-9b1d-0a7a1c1b9e11',
  email: 'ada@example.com',
  displayName: 'Ada',
  language: 'en-US',
  colorScheme: 'system',
  isAdmin: false,
}

describe('LanguageCard', () => {
  it('saves the Language the Member picks and shows it selected', async () => {
    let saved: unknown
    server.use(
      meHandler(ada),
      http.put('*/api/me/language', async ({ request }) => {
        saved = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderWithProviders(<LanguageCard />)

    const dropdown = await screen.findByLabelText('Language')
    await userEvent.selectOptions(dropdown, '🏴‍☠️ Ünïcödé Tëst')

    await waitFor(() => {
      expect(dropdown).toHaveValue('en-XA')
    })
    expect(saved).toEqual({ language: 'en-XA' })
  })

  it('renders nothing when nobody is signed in', async () => {
    server.use(signedOutHandler())

    const { container } = renderWithProviders(<LanguageCard />)

    await waitFor(() => {
      expect(container).toBeEmptyDOMElement()
    })
  })
})
