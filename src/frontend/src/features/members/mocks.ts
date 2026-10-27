import { http, HttpResponse } from 'msw'
import type {
  DormantMemberView,
  MemberDetailView,
  MemberRequestView,
  MemberSummaryView,
  PoolOverview,
} from './types'

export const sampleMember: MemberSummaryView = {
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
  id: sampleMember.id,
  displayName: sampleMember.displayName,
  email: sampleMember.email,
  lastActivityAt: '2026-01-05T09:00:00Z',
  availableQuarterCredits: 41,
}

export function membersHandler(items: MemberSummaryView[]) {
  return http.get('*/api/admin/members', () => HttpResponse.json({ items, nextCursor: null }))
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
