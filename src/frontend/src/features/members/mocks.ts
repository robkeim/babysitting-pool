import { http, HttpResponse } from 'msw'
import type { AdminMemberView, OffboardMemberResult } from './types'

export const sampleMember: AdminMemberView = {
  id: '11111111-1111-4111-8111-111111111111',
  displayName: 'Ada Lovelace',
  email: 'ada@example.com',
  isAdmin: false,
  createdAt: '2026-03-01T09:00:00Z',
  closedAt: null,
  isErased: false,
}

export function membersHandler(items: AdminMemberView[]) {
  return http.get('*/api/admin/members', () => HttpResponse.json({ items, nextCursor: null }))
}

export function membersFailureHandler() {
  return http.get('*/api/admin/members', () => problem(500, 'internal_error'))
}

export function offboardHandler(onOffboard: (id: string) => void, result: OffboardMemberResult) {
  return http.post('*/api/admin/members/:id/offboard', ({ params }) => {
    onOffboard(String(params.id))
    return HttpResponse.json(result)
  })
}

export function offboardRejectedHandler(status: number, code: string) {
  return http.post('*/api/admin/members/:id/offboard', () => problem(status, code))
}

export function eraseHandler(onErase: (id: string) => void) {
  return http.post('*/api/admin/members/:id/erase', ({ params }) => {
    onErase(String(params.id))
    return new HttpResponse(null, { status: 204 })
  })
}

export function eraseRejectedHandler(status: number, code: string) {
  return http.post('*/api/admin/members/:id/erase', () => problem(status, code))
}

function problem(status: number, code: string) {
  return HttpResponse.json(
    { status, title: code, code },
    { status, headers: { 'content-type': 'application/problem+json' } },
  )
}
