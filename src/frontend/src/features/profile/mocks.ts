import { http, HttpResponse } from 'msw'
import type { ProfileView } from './types'

export const sampleProfile: ProfileView = { area: 'Riverside', address: '12 River Rd' }

export function profileHandler(profile: ProfileView) {
  return http.get('*/api/me/profile', () => HttpResponse.json(profile))
}

/** Calls `onSave` with the body, then answers 200 with it as the new profile. */
export function updateProfileHandler(onSave: (body: ProfileView) => void) {
  return http.put('*/api/me/profile', async ({ request }) => {
    const body = (await request.json()) as ProfileView
    onSave(body)
    return HttpResponse.json(body)
  })
}

export function updateProfileRejectedHandler(code: string) {
  return http.put('*/api/me/profile', () =>
    HttpResponse.json(
      { status: 422, title: code, code },
      { status: 422, headers: { 'content-type': 'application/problem+json' } },
    ),
  )
}
