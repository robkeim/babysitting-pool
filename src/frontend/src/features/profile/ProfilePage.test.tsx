import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { ProfilePage } from './ProfilePage'
import {
  amiens,
  emptyProfile,
  profileHandler,
  sampleProfile,
  suggestionsHandler,
  suggestionsUnavailableHandler,
  updateProfileHandler,
  updateProfileRejectedHandler,
} from './mocks'
import type { ProfileUpdate } from './types'

async function search(user: ReturnType<typeof userEvent.setup>, text: string) {
  await user.type(await screen.findByRole('combobox', { name: /search for your address/i }), text)
}

describe('ProfilePage', () => {
  it('shows the saved address and the Area other members see', async () => {
    server.use(profileHandler(sampleProfile))

    renderWithProviders(<ProfilePage />)

    expect(await screen.findByRole('combobox')).toHaveValue('12 River Rd 80000 Riverside')
    expect(screen.getByText(/other members will see: riverside \(80\)/i)).toBeInTheDocument()
    expect(screen.getByRole('combobox')).toHaveAttribute('autocomplete', 'street-address')
    expect(screen.getByText(/only addresses in france/i)).toBeInTheDocument()
  })

  it('starts empty and cannot be saved for a Member who has not picked an address', async () => {
    server.use(profileHandler(emptyProfile))

    renderWithProviders(<ProfilePage />)

    expect(await screen.findByRole('combobox')).toHaveValue('')
    expect(screen.queryByText(/other members will see/i)).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /save/i })).toBeDisabled()
  })

  it('saves only a picked suggestion, previews its Area and sends the instructions', async () => {
    const user = userEvent.setup()
    const saved: ProfileUpdate[] = []
    const queries: string[] = []
    server.use(
      profileHandler(emptyProfile),
      suggestionsHandler([amiens], (q) => queries.push(q)),
      updateProfileHandler((b) => saved.push(b)),
    )

    renderWithProviders(<ProfilePage />)
    await search(user, '12 rue de la paix')
    expect(screen.getByRole('button', { name: /save/i })).toBeDisabled()
    expect(screen.getByText(/choose an address with a house number/i)).toBeInTheDocument()

    await user.click(await screen.findByRole('option', { name: amiens.label }))
    expect(screen.getByText(/other members will see: amiens \(80\)/i)).toBeInTheDocument()
    await user.type(screen.getByLabelText(/instructions/i), 'apt 3')
    await user.click(screen.getByRole('button', { name: /save/i }))

    expect(await screen.findByRole('status')).toHaveTextContent(/saved/i)
    expect(saved).toEqual([{ banId: amiens.banId, label: amiens.label, instructions: 'apt 3' }])
    // Debounced: one search for the whole text, none per keystroke.
    expect(queries).toEqual(['12 rue de la paix'])
  })

  it('picks a suggestion with the keyboard', async () => {
    const user = userEvent.setup()
    const saved: ProfileUpdate[] = []
    server.use(
      profileHandler(emptyProfile),
      suggestionsHandler([amiens]),
      updateProfileHandler((b) => saved.push(b)),
    )

    renderWithProviders(<ProfilePage />)
    await search(user, 'rue de la paix')
    await screen.findByRole('option')
    await user.keyboard('{ArrowDown}{Enter}')
    await user.click(screen.getByRole('button', { name: /save/i }))

    expect(await screen.findByRole('status')).toBeInTheDocument()
    expect(saved[0]?.banId).toBe(amiens.banId)
  })

  it('does not search for fewer than 3 characters', async () => {
    const user = userEvent.setup()
    const queries: string[] = []
    server.use(
      profileHandler(emptyProfile),
      suggestionsHandler([amiens], (q) => queries.push(q)),
    )

    renderWithProviders(<ProfilePage />)
    await search(user, '12')
    await new Promise((resolve) => setTimeout(resolve, 500))

    expect(queries).toEqual([])
    expect(screen.queryByRole('option')).not.toBeInTheDocument()
  })

  it('says when no address matches', async () => {
    const user = userEvent.setup()
    server.use(profileHandler(emptyProfile), suggestionsHandler([]))

    renderWithProviders(<ProfilePage />)
    await search(user, 'rue de la paix')

    expect(await screen.findByText(/no address found/i)).toBeInTheDocument()
  })

  it('says when the address search is unavailable and blocks the change', async () => {
    const user = userEvent.setup()
    server.use(profileHandler(emptyProfile), suggestionsUnavailableHandler())

    renderWithProviders(<ProfilePage />)
    await search(user, 'rue de la paix')

    expect(await screen.findByText(/address search is unavailable/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /save/i })).toBeDisabled()
  })

  it('saves new instructions without picking the saved address again', async () => {
    const user = userEvent.setup()
    const saved: ProfileUpdate[] = []
    server.use(
      profileHandler(sampleProfile),
      updateProfileHandler((b) => saved.push(b)),
    )

    renderWithProviders(<ProfilePage />)
    await user.type(await screen.findByLabelText(/instructions/i), 'back door')
    await user.click(screen.getByRole('button', { name: /save/i }))

    expect(await screen.findByRole('status')).toBeInTheDocument()
    expect(saved).toEqual([{ instructions: 'back door' }])
  })

  it('explains a rejected address', async () => {
    const user = userEvent.setup()
    server.use(profileHandler(sampleProfile), updateProfileRejectedHandler('invalid_address'))

    renderWithProviders(<ProfilePage />)
    await screen.findByRole('combobox')
    await user.click(screen.getByRole('button', { name: /save/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/house number/i)
  })

  it('says nothing was changed when the service is down at save time', async () => {
    const user = userEvent.setup()
    server.use(
      profileHandler(sampleProfile),
      updateProfileRejectedHandler('address_service_unavailable'),
    )

    renderWithProviders(<ProfilePage />)
    await screen.findByRole('combobox')
    await user.click(screen.getByRole('button', { name: /save/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/nothing was changed/i)
  })
})
