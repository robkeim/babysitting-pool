import { http, HttpResponse } from 'msw'
import type {
  CostQuote,
  OpenRequestPage,
  OpenRequestView,
  PostRequestBody,
  RequestPage,
  RequestView,
} from './types'

export const sampleRequest: RequestView = {
  id: '2f6c1c3e-5d8a-4a38-9b1d-0a7a1c1b9e11',
  startsAt: '2026-03-02T10:00:00Z',
  durationMinutes: 120,
  childCount: 1,
  location: 'SittersHouse',
  transport: 'None',
  lunch: false,
  snack: false,
  dinner: false,
  area: 'Riverside',
  address: '12 River Rd',
  costQuarterCredits: 8,
  claimCutoffAt: '2026-03-02T07:00:00Z',
  postedAt: '2026-03-01T09:00:00Z',
  status: 'Posted',
}

export function myRequestsHandler(page: RequestPage) {
  return http.get('*/api/me/requests', () => HttpResponse.json(page))
}

export function myRequestsFailureHandler() {
  return http.get('*/api/me/requests', () =>
    HttpResponse.json(
      { status: 500, title: 'Boom', code: 'internal_error' },
      { status: 500, headers: { 'content-type': 'application/problem+json' } },
    ),
  )
}

const zeroQuote: CostQuote = {
  costQuarterCredits: 0,
  lines: {
    hourlyRateQuarterCredits: 4,
    timeQuarterCredits: 0,
    locationQuarterCredits: 0,
    transportQuarterCredits: 0,
    lunchQuarterCredits: 0,
    snackQuarterCredits: 0,
    dinnerQuarterCredits: 0,
  },
  options: {
    familyHouseQuarterCredits: 2,
    oneWayTransportQuarterCredits: 2,
    roundTripTransportQuarterCredits: 3,
    lunchQuarterCredits: 2,
    snackQuarterCredits: 1,
    dinnerQuarterCredits: 2,
    lunchPerChildQuarterCredits: 2,
    snackPerChildQuarterCredits: 1,
    dinnerPerChildQuarterCredits: 2,
    secondChildHourlyQuarterCredits: 2,
    additionalChildHourlyQuarterCredits: 1,
  },
}

/** Answers every quote with the same itemised cost; unset parts are the one-child defaults. */
export function quoteHandler(quote: Partial<CostQuote> & Pick<CostQuote, 'costQuarterCredits'>) {
  return http.get('*/api/requests/quote', () => HttpResponse.json({ ...zeroQuote, ...quote }))
}

/** Calls `onPost` with the body, then answers 201. */
export function postRequestHandler(onPost: (body: PostRequestBody) => void) {
  return http.post('*/api/requests', async ({ request }) => {
    onPost((await request.json()) as PostRequestBody)
    return HttpResponse.json(sampleRequest, { status: 201 })
  })
}

export function postRequestRejectedHandler(code: string) {
  return http.post('*/api/requests', () =>
    HttpResponse.json(
      { status: 422, title: code, code },
      { status: 422, headers: { 'content-type': 'application/problem+json' } },
    ),
  )
}

/** The Member's balances as the post form reads them from the Credit Account. */
export function availableBalanceHandler(availableQuarterCredits: number) {
  return http.get('*/api/me/account', () =>
    HttpResponse.json({
      availableQuarterCredits,
      escrowedQuarterCredits: 0,
      ledger: { items: [], nextCursor: null },
    }),
  )
}

export const sampleOpenRequest: OpenRequestView = {
  id: '9d1f2c3e-5d8a-4a38-9b1d-0a7a1c1b9e22',
  startsAt: '2026-03-02T10:00:00Z',
  durationMinutes: 120,
  childCount: 2,
  location: 'SittersHouse',
  transport: 'None',
  lunch: false,
  snack: false,
  dinner: false,
  area: 'Riverside',
  costQuarterCredits: 12,
  claimCutoffAt: '2026-03-02T07:00:00Z',
}

export function openRequestsHandler(page: OpenRequestPage) {
  return http.get('*/api/requests/open', () => HttpResponse.json(page))
}

export function openRequestsFailureHandler() {
  return http.get('*/api/requests/open', () =>
    HttpResponse.json(
      { status: 500, title: 'Boom', code: 'internal_error' },
      { status: 500, headers: { 'content-type': 'application/problem+json' } },
    ),
  )
}

export function myClaimsHandler(page: RequestPage) {
  return http.get('*/api/me/claims', () => HttpResponse.json(page))
}

export function myClaimsFailureHandler() {
  return http.get('*/api/me/claims', () =>
    HttpResponse.json(
      { status: 500, title: 'Boom', code: 'internal_error' },
      { status: 500, headers: { 'content-type': 'application/problem+json' } },
    ),
  )
}

/** Calls `onClaim` with the Request id, then answers 200 with the claimed Request. */
export function claimRequestHandler(onClaim: (requestId: string) => void) {
  return http.post('*/api/requests/:id/claim', ({ params }) => {
    onClaim(String(params.id))
    return HttpResponse.json({ ...sampleRequest, status: 'Claimed' })
  })
}

export function claimRequestRejectedHandler(status: number, code: string) {
  return http.post('*/api/requests/:id/claim', () =>
    HttpResponse.json(
      { status, title: code, code },
      { status, headers: { 'content-type': 'application/problem+json' } },
    ),
  )
}

/** Calls `onRelease` with the Request id, then answers 200 with the Request in `status`. */
export function releaseRequestHandler(
  action: 'cancel' | 'withdraw',
  status: RequestView['status'],
  onRelease: (requestId: string) => void,
) {
  return http.post(`*/api/requests/:id/${action}`, ({ params }) => {
    onRelease(String(params.id))
    return HttpResponse.json({ ...sampleRequest, status })
  })
}

export function releaseRequestRejectedHandler(
  action: 'cancel' | 'withdraw',
  status: number,
  code: string,
) {
  return http.post(`*/api/requests/:id/${action}`, () =>
    HttpResponse.json(
      { status, title: code, code },
      { status, headers: { 'content-type': 'application/problem+json' } },
    ),
  )
}

/** The Member's profile as the post form reads it: where a Request at their house takes its area and address from. */
export function profileHandler(profile: { area: string | null; address: string | null }) {
  return http.get('*/api/me/profile', () => HttpResponse.json(profile))
}

export const sampleProfile = { area: 'Riverside', address: '12 River Rd' }
