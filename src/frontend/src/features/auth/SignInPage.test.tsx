import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { problem } from './mocks'
import { SignInPage } from './SignInPage'

describe('SignInPage', () => {
  it('asks for a link and always answers that one was sent if the address is a Member', async () => {
    let body: unknown
    let csrf: string | null = null
    server.use(
      http.post('*/api/auth/sign-in-links', async ({ request }) => {
        body = await request.json()
        csrf = request.headers.get('x-requested-with')
        return new HttpResponse(null, { status: 202 })
      }),
    )
    renderWithProviders(<SignInPage />)

    await userEvent.type(screen.getByLabelText('Email address'), 'ada@example.com')
    await userEvent.click(screen.getByRole('button', { name: 'Email me a sign-in link' }))

    expect(await screen.findByText(/If that address belongs to a Member/)).toBeInTheDocument()
    expect(body).toEqual({ email: 'ada@example.com' })
    expect(csrf).toBe('fetch')
  })

  it('tells the person when the address is rejected', async () => {
    server.use(http.post('*/api/auth/sign-in-links', () => problem(400, 'invalid_request')))
    renderWithProviders(<SignInPage />)

    await userEvent.type(screen.getByLabelText('Email address'), 'ada@example.com')
    await userEvent.click(screen.getByRole('button', { name: 'Email me a sign-in link' }))

    expect(await screen.findByText(/doesn't look like an email address/)).toBeInTheDocument()
  })
})
