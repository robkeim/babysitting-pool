import { http, HttpResponse } from 'msw'
import type { FeatureFlags, FeatureFlagView, UpdateFeatureFlagRequest } from './types'

export function featureFlagsHandler(flags: FeatureFlags) {
  return http.get('*/api/feature-flags', () => HttpResponse.json(flags))
}

export const sampleFlag: FeatureFlagView = {
  name: 'NewThing',
  defaultEnabled: false,
  removalIssue: 42,
  enabled: false,
  rolloutPercent: 0,
  allowedMemberIds: [],
  deniedMemberIds: [],
  roleRollouts: [],
}

export function adminFlagsHandler(items: FeatureFlagView[]) {
  return http.get('*/api/admin/feature-flags', () => HttpResponse.json({ items, nextCursor: null }))
}

export function adminFlagsFailureHandler() {
  return http.get('*/api/admin/feature-flags', () =>
    HttpResponse.json(
      { status: 500, title: 'Boom', code: 'internal_error' },
      { status: 500, headers: { 'content-type': 'application/problem+json' } },
    ),
  )
}

export function updateFlagHandler(onUpdate: (body: UpdateFeatureFlagRequest) => void) {
  return http.put('*/api/admin/feature-flags', async ({ request }) => {
    const body = (await request.json()) as UpdateFeatureFlagRequest
    onUpdate(body)
    return HttpResponse.json({ ...sampleFlag, ...body })
  })
}

export function updateFlagRejectedHandler(status: number, code: string) {
  return http.put('*/api/admin/feature-flags', () =>
    HttpResponse.json(
      { status, title: code, code },
      { status, headers: { 'content-type': 'application/problem+json' } },
    ),
  )
}
