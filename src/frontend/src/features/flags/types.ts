import type { components, operations } from '@/lib/api/schema.gen'

/** Flags resolved for the caller, by name (ADR 0016). The backend registry is the only place they are defined. */
export type FeatureFlags =
  operations['GetFeatureFlags']['responses']['200']['content']['application/json']

export type FeatureFlagView = components['schemas']['FeatureFlagView']
export type UpdateFeatureFlagRequest = components['schemas']['UpdateFeatureFlagRequest']

export interface Page<T> {
  items: T[]
  nextCursor: string | null
}
