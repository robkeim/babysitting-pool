import { useEffect } from 'react'
import { useFeatureFlagValue } from './api'

const VACATION_FLAG = 'vacationMode'
const STORAGE_KEY = 'theme'

function apply(theme: 'vacation' | undefined) {
  if (theme) document.documentElement.dataset.theme = theme
  else delete document.documentElement.dataset.theme
}

// Browser storage can throw (private windows, blocked site data), and the theme must work without it.
function remember(on: boolean) {
  try {
    if (on) localStorage.setItem(STORAGE_KEY, 'vacation')
    else localStorage.removeItem(STORAGE_KEY)
  } catch {
    // The theme still applies for this page load.
  }
}

/** Applies the last theme before the first render, so a Member does not see the default colors flash first. */
export function restoreTheme() {
  try {
    apply(localStorage.getItem(STORAGE_KEY) === 'vacation' ? 'vacation' : undefined)
  } catch {
    apply(undefined)
  }
}

/** Sets the theme from the `vacationMode` flag (ADR 0017). Leaves it alone while the flags load. */
export function useFlagTheme() {
  const vacation = useFeatureFlagValue(VACATION_FLAG)
  useEffect(() => {
    if (vacation === undefined) return
    apply(vacation ? 'vacation' : undefined)
    remember(vacation)
  }, [vacation])
}
