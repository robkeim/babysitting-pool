import { http, HttpResponse } from 'msw'
import type { MemberView } from './types'

export function problem(status: number, code: string) {
  return HttpResponse.json(
    { status, title: code, code },
    { status, headers: { 'content-type': 'application/problem+json' } },
  )
}

export function meHandler(member: MemberView) {
  return http.get('*/api/me', () => HttpResponse.json(member))
}

export function signedOutHandler() {
  return http.get('*/api/me', () => problem(401, 'unauthenticated'))
}
