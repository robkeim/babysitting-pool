import { http, HttpResponse } from 'msw'
import type { CostQuote, PostRequestBody, RequestPage, RequestView } from './types'

export const sampleRequest: RequestView = {
  id: '2f6c1c3e-5d8a-4a38-9b1d-0a7a1c1b9e11',
  startsAt: '2026-03-02T10:00:00Z',
  durationMinutes: 120,
  childCount: 1,
  location: 'SittersHouse',
  transport: 'None',
  lunchOrDinner: false,
  snack: false,
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

/** Answers every quote with the same cost. */
export function quoteHandler(quote: CostQuote) {
  return http.get('*/api/requests/quote', () => HttpResponse.json(quote))
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
