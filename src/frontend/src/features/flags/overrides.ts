import { createContext, useContext } from 'react'
import type { FeatureFlags } from './types'

export const OverridesContext = createContext<FeatureFlags | undefined>(undefined)

/** Flags a test set explicitly; undefined outside a test provider, where the hook asks the API. */
export function useFeatureFlagOverrides(): FeatureFlags | undefined {
  return useContext(OverridesContext)
}
