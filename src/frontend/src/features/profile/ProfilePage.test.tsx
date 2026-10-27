import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { ProfilePage } from './ProfilePage'
import {
  profileHandler,
  sampleProfile,
  updateProfileHandler,
  updateProfileRejectedHandler,
} from './mocks'
import type { ProfileView } from './types'

describe('ProfilePage', () => {
  it('shows the saved area and address', async () => {
    server.use(profileHandler(sampleProfile))

    renderWithProviders(<ProfilePage />)

    expect(await screen.findByLabelText(/^area/i)).toHaveValue('Riverside')
    expect(screen.getByLabelText(/exact address/i)).toHaveValue('12 River Rd')
  })

  it('starts empty for a Member who has not set them', async () => {
    server.use(profileHandler({ area: null, address: null }))

    renderWithProviders(<ProfilePage />)

    expect(await screen.findByLabelText(/^area/i)).toHaveValue('')
    expect(screen.getByLabelText(/exact address/i)).toHaveValue('')
  })

  it('saves the area and address', async () => {
    const user = userEvent.setup()
    const saved: ProfileView[] = []
    server.use(
      profileHandler({ area: null, address: null }),
      updateProfileHandler((b) => saved.push(b)),
    )

    renderWithProviders(<ProfilePage />)
    await user.type(await screen.findByLabelText(/^area/i), 'Hilltop')
    await user.type(screen.getByLabelText(/exact address/i), '1 Hill St')
    await user.click(screen.getByRole('button', { name: /save/i }))

    expect(await screen.findByRole('status')).toHaveTextContent(/saved/i)
    expect(saved).toEqual([{ area: 'Hilltop', address: '1 Hill St' }])
  })

  it('explains a rejected address', async () => {
    const user = userEvent.setup()
    server.use(profileHandler(sampleProfile), updateProfileRejectedHandler('invalid_address'))

    renderWithProviders(<ProfilePage />)
    await screen.findByLabelText(/^area/i)
    await user.click(screen.getByRole('button', { name: /save/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/address of at most 200/i)
  })
})
