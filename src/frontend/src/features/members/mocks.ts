import { http, HttpResponse } from 'msw'
import type {
  AdminMemberView,
  DormantMemberView,
  MemberDetailView,
  MemberRequestView,
  MemberSummaryView,
  OffboardMemberResult,
  PoolOverview,
} from './types'

export const sampleMember: AdminMemberView = {
  id: '11111111-1111-4111-8111-111111111111',
  displayName: 'Ada Lovelace',
  email: 'ada@example.com',
  isAdmin: false,
  createdAt: '2026-03-01T09:00:00Z',
  closedAt: null,
  isErased: false,
}

export const sampleSummary: MemberSummaryView = {
  id: '11111111-1111-4111-8111-111111111111',
  displayName: 'Ada Lovelace',
  email: 'ada@example.com',
  isAdmin: false,
  createdAt: '2026-01-05T09:00:00Z',
  closedAt: null,
  availableQuarterCredits: 41,
  escrowedQuarterCredits: 8,
}

export const sampleOverview: PoolOverview = {
  creditsInCirculationQuarterCredits: 400,
  escrowedQuarterCredits: 24,
  burnedByDemurrageQuarterCredits: 16,
  burnedByOffboardingQuarterCredits: 40,
  openMembers: 7,
  requests: { posted: 3, claimed: 2, completed: 11, cancelled: 4, expired: 1 },
}

export const sampleDormant: DormantMemberView = {
  id: sampleSummary.id,
  displayName: sampleSummary.displayName,
  email: sampleSummary.email,
  lastActivityAt: '2026-01-05T09:00:00Z',
  availableQuarterCredits: 41,
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

export function memberDetailHandler(detail: MemberDetailView) {
  return http.get('*/api/admin/members/:id', () => HttpResponse.json(detail))
}

export function memberRequestsHandler(items: MemberRequestView[]) {
  return http.get('*/api/admin/members/:id/requests', () =>
    HttpResponse.json({ items, nextCursor: null }),
  )
}

export function poolOverviewHandler(overview: PoolOverview) {
  return http.get('*/api/admin/pool-overview', () => HttpResponse.json(overview))
}

export function dormantHandler(items: DormantMemberView[]) {
  return http.get('*/api/admin/dormant-members', () =>
    HttpResponse.json({ items, nextCursor: null }),
  )
}

export function failureHandler(path: string) {
  return http.get(path, () =>
    HttpResponse.json(
      { status: 500, title: 'internal_error', code: 'internal_error' },
      { status: 500, headers: { 'content-type': 'application/problem+json' } },
    ),
  )
}

function problem(status: number, code: string) {
  return HttpResponse.json(
    { status, title: code, code },
    { status, headers: { 'content-type': 'application/problem+json' } },
  )
}
