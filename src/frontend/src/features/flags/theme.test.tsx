import { screen } from '@testing-library/react'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { FeatureFlagsTestProvider } from './FeatureFlagsTestProvider'
import { featureFlagsHandler } from './mocks'
import { restoreTheme, useFlagTheme } from './theme'

function Probe() {
  useFlagTheme()
  return <p>probe</p>
}

describe('vacation theme', () => {
  beforeEach(() => {
    localStorage.clear()
    delete document.documentElement.dataset.theme
  })

  it('applies the theme while the vacationMode flag is on and remembers it', async () => {
    renderWithProviders(
      <FeatureFlagsTestProvider flags={{ vacationMode: true }}>
        <Probe />
      </FeatureFlagsTestProvider>,
    )

    await screen.findByText('probe')
    expect(document.documentElement.dataset.theme).toBe('vacation')
    expect(localStorage.getItem('theme')).toBe('vacation')
  })

  it('drops the theme when the flag goes off', async () => {
    localStorage.setItem('theme', 'vacation')
    restoreTheme()
    renderWithProviders(
      <FeatureFlagsTestProvider flags={{ vacationMode: false }}>
        <Probe />
      </FeatureFlagsTestProvider>,
    )

    await screen.findByText('probe')
    expect(document.documentElement.dataset.theme).toBeUndefined()
    expect(localStorage.getItem('theme')).toBeNull()
  })

  it('keeps the remembered theme while the flags are still loading', async () => {
    localStorage.setItem('theme', 'vacation')
    restoreTheme()
    server.use(featureFlagsHandler({ vacationMode: true }))
    renderWithProviders(<Probe />)

    expect(document.documentElement.dataset.theme).toBe('vacation')
    await screen.findByText('probe')
    expect(document.documentElement.dataset.theme).toBe('vacation')
  })

  it('restores nothing when nothing was remembered', () => {
    restoreTheme()

    expect(document.documentElement.dataset.theme).toBeUndefined()
  })
})
