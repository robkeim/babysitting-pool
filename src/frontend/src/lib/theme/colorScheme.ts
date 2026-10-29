import { useEffect } from 'react'

export const COLOR_SCHEMES = ['system', 'light', 'dark'] as const
export type ColorScheme = (typeof COLOR_SCHEMES)[number]

const STORAGE_KEY = 'colorScheme'
const DARK_QUERY = '(prefers-color-scheme: dark)'

function parse(value: string | null | undefined): ColorScheme {
  return COLOR_SCHEMES.find((scheme) => scheme === value) ?? 'system'
}

// Dark is the `dark` class on <html> (ADR 0017); `color-scheme` follows it so native controls match an explicit choice.
function apply(scheme: ColorScheme) {
  const root = document.documentElement
  const dark = scheme === 'dark' || (scheme === 'system' && window.matchMedia(DARK_QUERY).matches)
  root.classList.toggle('dark', dark)
  root.style.colorScheme = scheme === 'system' ? '' : scheme
}

// Browser storage can throw (private windows, blocked site data), and the scheme must work without it.
function remember(scheme: ColorScheme) {
  try {
    localStorage.setItem(STORAGE_KEY, scheme)
  } catch {
    // The scheme still applies for this page load.
  }
}

/** Applies the last Colour Scheme before the first render, so a Member does not see the wrong one flash first. */
export function restoreColorScheme() {
  try {
    apply(parse(localStorage.getItem(STORAGE_KEY)))
  } catch {
    apply('system')
  }
}

/**
 * Shows the app in the signed-in Member's Colour Scheme and, while it is `system`, follows the browser or OS setting
 * as it changes. Leaves the remembered scheme alone while there is no Member.
 */
export function useColorScheme(value: string | undefined) {
  useEffect(() => {
    if (value === undefined) return
    const scheme = parse(value)
    apply(scheme)
    remember(scheme)
    if (scheme !== 'system') return
    const query = window.matchMedia(DARK_QUERY)
    const onChange = () => {
      apply('system')
    }
    query.addEventListener('change', onChange)
    return () => {
      query.removeEventListener('change', onChange)
    }
  }, [value])
}
