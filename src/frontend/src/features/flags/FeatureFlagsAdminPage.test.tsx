import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { FeatureFlagsAdminPage } from './FeatureFlagsAdminPage'
import {
  adminFlagsFailureHandler,
  adminFlagsHandler,
  sampleFlag,
  updateFlagHandler,
  updateFlagRejectedHandler,
} from './mocks'
import type { UpdateFeatureFlagRequest } from './types'

const MEMBER = '11111111-1111-4111-8111-111111111111'

describe('FeatureFlagsAdminPage', () => {
  it('lists each flag with its stored settings', async () => {
    server.use(
      adminFlagsHandler([
        {
          ...sampleFlag,
          enabled: true,
          rolloutPercent: 25,
          allowedMemberIds: [MEMBER],
          roleRollouts: [{ role: 'Admin', rolloutPercent: 100 }],
        },
      ]),
    )

    renderWithProviders(<FeatureFlagsAdminPage />)

    const card = await screen.findByRole('group', { name: 'NewThing' })
    expect(within(card).getByText('Removed by issue #42')).toBeInTheDocument()
    expect(within(card).getByRole('checkbox', { name: /enabled/i })).toBeChecked()
    expect(within(card).getByLabelText('Rollout percent')).toHaveValue('25')
    expect(within(card).getByLabelText('Admin rollout percent')).toHaveValue('100')
    expect(within(card).getByLabelText('Member rollout percent')).toHaveValue('0')
    expect(within(card).getByLabelText(/always on/i)).toHaveValue(MEMBER)
  })

  it('saves the edited settings whole', async () => {
    const user = userEvent.setup()
    const saved: UpdateFeatureFlagRequest[] = []
    server.use(
      adminFlagsHandler([sampleFlag]),
      updateFlagHandler((body) => saved.push(body)),
    )

    renderWithProviders(<FeatureFlagsAdminPage />)
    await screen.findByRole('group', { name: 'NewThing' })
    await user.click(screen.getByRole('checkbox', { name: /enabled/i }))
    await user.clear(screen.getByLabelText('Rollout percent'))
    await user.type(screen.getByLabelText('Rollout percent'), '10')
    await user.type(screen.getByLabelText('Member rollout percent'), '{Backspace}50')
    await user.type(screen.getByLabelText(/always off/i), MEMBER)
    await user.click(screen.getByRole('button', { name: 'Save NewThing' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Saved.')
    expect(saved).toEqual([
      {
        name: 'NewThing',
        enabled: true,
        rolloutPercent: 10,
        allowedMemberIds: [],
        deniedMemberIds: [MEMBER],
        roleRollouts: [{ role: 'Member', rolloutPercent: 50 }],
      },
    ])
  })

  it('blocks saving a rollout outside 0 to 100', async () => {
    const user = userEvent.setup()
    server.use(adminFlagsHandler([sampleFlag]))

    renderWithProviders(<FeatureFlagsAdminPage />)
    await screen.findByRole('group', { name: 'NewThing' })
    await user.clear(screen.getByLabelText('Rollout percent'))
    await user.type(screen.getByLabelText('Rollout percent'), '101')

    expect(screen.getByRole('alert')).toHaveTextContent(/0 to 100/i)
    expect(screen.getByRole('button', { name: 'Save NewThing' })).toBeDisabled()
  })

  it('blocks saving a member list that is not ids', async () => {
    const user = userEvent.setup()
    server.use(adminFlagsHandler([sampleFlag]))

    renderWithProviders(<FeatureFlagsAdminPage />)
    await screen.findByRole('group', { name: 'NewThing' })
    await user.type(screen.getByLabelText(/always on/i), 'alice')

    expect(screen.getByRole('alert')).toHaveTextContent(/member id/i)
    expect(screen.getByRole('button', { name: 'Save NewThing' })).toBeDisabled()
  })

  it('says so when a flag no longer exists', async () => {
    const user = userEvent.setup()
    server.use(
      adminFlagsHandler([sampleFlag]),
      updateFlagRejectedHandler(404, 'feature_flag_not_found'),
    )

    renderWithProviders(<FeatureFlagsAdminPage />)
    await screen.findByRole('group', { name: 'NewThing' })
    await user.click(screen.getByRole('button', { name: 'Save NewThing' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/no longer exists/i)
  })

  it('says so when the flags cannot be loaded', async () => {
    server.use(adminFlagsFailureHandler())

    renderWithProviders(<FeatureFlagsAdminPage />)

    expect(await screen.findByRole('alert')).toHaveTextContent(/couldn't load the feature flags/i)
  })
})
