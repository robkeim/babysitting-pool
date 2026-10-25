import type { ReactNode } from 'react'
import { OverridesContext } from './overrides'
import type { FeatureFlags } from './types'

/** For unit tests: `useFeatureFlag` reads these values and never calls the API. A flag not listed is off. */
export function FeatureFlagsTestProvider({
  flags,
  children,
}: {
  flags: FeatureFlags
  children: ReactNode
}) {
  return <OverridesContext value={flags}>{children}</OverridesContext>
}
