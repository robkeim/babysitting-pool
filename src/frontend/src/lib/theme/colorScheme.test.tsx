import { act, render } from '@testing-library/react'
import { COLOR_SCHEMES, restoreColorScheme, useColorScheme } from './colorScheme'

function Probe({ scheme }: { scheme: string | undefined }) {
  useColorScheme(scheme)
  return null
}

const root = document.documentElement

/** A controllable `prefers-color-scheme: dark` media query. */
function stubSystem(initiallyDark: boolean) {
  let dark = initiallyDark
  const listeners = new Set<() => void>()
  vi.stubGlobal('matchMedia', () => ({
    get matches() {
      return dark
    },
    addEventListener: (_: string, listener: () => void) => listeners.add(listener),
    removeEventListener: (_: string, listener: () => void) => listeners.delete(listener),
  }))
  return {
    listeners,
    set(next: boolean) {
      dark = next
      act(() => {
        listeners.forEach((listener) => {
          listener()
        })
      })
    },
  }
}

describe('color scheme', () => {
  beforeEach(() => {
    localStorage.clear()
    root.classList.remove('dark')
    root.style.colorScheme = ''
  })
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('offers system, light and dark', () => {
    expect(COLOR_SCHEMES).toEqual(['system', 'light', 'dark'])
  })

  it.each([
    { scheme: 'light', system: true, dark: false },
    { scheme: 'dark', system: false, dark: true },
    { scheme: 'system', system: true, dark: true },
    { scheme: 'system', system: false, dark: false },
  ])('shows dark=$dark for $scheme when the system is dark=$system', ({ scheme, system, dark }) => {
    stubSystem(system)

    render(<Probe scheme={scheme} />)

    expect(root.classList.contains('dark')).toBe(dark)
    expect(localStorage.getItem('colorScheme')).toBe(scheme)
  })

  it('keeps native controls on an explicit choice and on the system otherwise', () => {
    stubSystem(false)
    const { rerender } = render(<Probe scheme="dark" />)
    expect(root.style.colorScheme).toBe('dark')

    rerender(<Probe scheme="system" />)
    expect(root.style.colorScheme).toBe('')
  })

  it('follows the system while it is chosen, and stops once an explicit scheme is', () => {
    const system = stubSystem(false)
    const { rerender } = render(<Probe scheme="system" />)

    system.set(true)
    expect(root.classList.contains('dark')).toBe(true)
    system.set(false)
    expect(root.classList.contains('dark')).toBe(false)

    rerender(<Probe scheme="light" />)
    expect(system.listeners.size).toBe(0)
    system.set(true)
    expect(root.classList.contains('dark')).toBe(false)
  })

  it('leaves the restored scheme alone while there is no Member', () => {
    stubSystem(false)
    localStorage.setItem('colorScheme', 'dark')
    restoreColorScheme()

    render(<Probe scheme={undefined} />)

    expect(root.classList.contains('dark')).toBe(true)
  })

  it('restores the remembered scheme, or the system setting when nothing was remembered', () => {
    stubSystem(true)
    restoreColorScheme()
    expect(root.classList.contains('dark')).toBe(true)

    localStorage.setItem('colorScheme', 'light')
    restoreColorScheme()
    expect(root.classList.contains('dark')).toBe(false)
  })

  it('treats an unknown remembered value as the system setting', () => {
    stubSystem(true)
    localStorage.setItem('colorScheme', 'sepia')

    restoreColorScheme()

    expect(root.classList.contains('dark')).toBe(true)
  })

  it('is independent of the vacation theme', () => {
    stubSystem(false)
    root.dataset.theme = 'vacation'

    render(<Probe scheme="dark" />)

    expect(root.dataset.theme).toBe('vacation')
    expect(root.classList.contains('dark')).toBe(true)
    delete root.dataset.theme
  })
})
