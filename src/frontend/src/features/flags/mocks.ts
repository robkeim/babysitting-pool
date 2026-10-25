import { http, HttpResponse } from 'msw'
import type { FeatureFlags } from './types'

export function featureFlagsHandler(flags: FeatureFlags) {
  return http.get('*/api/feature-flags', () => HttpResponse.json(flags))
}
