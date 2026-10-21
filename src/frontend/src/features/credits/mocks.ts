import { http, HttpResponse } from 'msw'
import type { AccountView } from './types'

export function accountHandler(account: AccountView) {
  return http.get('*/api/me/account', () => HttpResponse.json(account))
}

export function accountFailureHandler() {
  return http.get('*/api/me/account', () =>
    HttpResponse.json(
      { status: 500, title: 'Boom', code: 'internal_error' },
      { status: 500, headers: { 'content-type': 'application/problem+json' } },
    ),
  )
}

/** Serves one page per cursor; the first page is keyed by the empty string. */
export function accountPagesHandler(pages: Record<string, AccountView>) {
  return http.get('*/api/me/account', ({ request }) => {
    const cursor = new URL(request.url).searchParams.get('cursor') ?? ''
    return HttpResponse.json(pages[cursor])
  })
}
