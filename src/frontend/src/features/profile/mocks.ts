import { http, HttpResponse } from 'msw'
import type { AddressSuggestion, ProfileUpdate, ProfileView } from './types'

export const emptyProfile: ProfileView = {
  area: null,
  address: null,
  commune: null,
  inseeCode: null,
  departmentNumber: null,
  postcode: null,
  instructions: null,
}

export const sampleProfile: ProfileView = {
  area: 'Riverside (80)',
  address: '12 River Rd 80000 Riverside',
  commune: 'Riverside',
  inseeCode: '80001',
  departmentNumber: '80',
  postcode: '80000',
  instructions: null,
}

export const amiens: AddressSuggestion = {
  banId: '80021_6115_12',
  // A BAN label is address data, not a translatable message.
  // eslint-disable-next-line formatjs/no-literal-string-in-object
  label: '12 Rue de la Paix 80000 Amiens',
  commune: 'Amiens',
  inseeCode: '80021',
  departmentNumber: '80',
  postcode: '80000',
  type: 'housenumber',
}

export function profileHandler(profile: ProfileView) {
  return http.get('*/api/me/profile', () => HttpResponse.json(profile))
}

/** Answers every search with `items`, and calls `onSearch` with the query text. */
export function suggestionsHandler(items: AddressSuggestion[], onSearch?: (query: string) => void) {
  return http.get('*/api/address-suggestions', ({ request }) => {
    onSearch?.(new URL(request.url).searchParams.get('q') ?? '')
    return HttpResponse.json({ items, nextCursor: null })
  })
}

export function suggestionsUnavailableHandler() {
  return http.get('*/api/address-suggestions', () =>
    HttpResponse.json(
      { status: 503, title: 'down', code: 'address_service_unavailable' },
      { status: 503, headers: { 'content-type': 'application/problem+json' } },
    ),
  )
}

/** Calls `onSave` with the body, then answers 200 with the profile it would produce. */
export function updateProfileHandler(onSave: (body: ProfileUpdate) => void) {
  return http.put('*/api/me/profile', async ({ request }) => {
    const body = (await request.json()) as ProfileUpdate
    onSave(body)
    return HttpResponse.json({ ...sampleProfile, instructions: body.instructions })
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
