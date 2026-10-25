import { http, HttpResponse } from 'msw'
import type { InvitationView } from './types'

export const sampleInvitation: InvitationView = {
  id: '11111111-1111-4111-8111-111111111111',
  email: 'ada@example.com',
  isAdmin: false,
  status: 'Pending',
  issuedAt: '2026-03-01T09:00:00Z',
  expiresAt: '2026-03-08T09:00:00Z',
  acceptedAt: null,
}

export function invitationsHandler(items: InvitationView[]) {
  return http.get('*/api/admin/invitations', () => HttpResponse.json({ items, nextCursor: null }))
}

export function invitationsFailureHandler() {
  return http.get('*/api/admin/invitations', () => problem(500, 'internal_error'))
}

export function inviteHandler(onInvite: (email: string) => void) {
  return http.post('*/api/admin/invitations', async ({ request }) => {
    const body = (await request.json()) as { email: string }
    onInvite(body.email)
    return HttpResponse.json({ ...sampleInvitation, email: body.email }, { status: 201 })
  })
}

export function inviteRejectedHandler(status: number, code: string) {
  return http.post('*/api/admin/invitations', () => problem(status, code))
}

export function resendHandler(onResend: (id: string) => void) {
  return http.post('*/api/admin/invitations/:id/resend', ({ params }) => {
    onResend(String(params.id))
    return HttpResponse.json(sampleInvitation)
  })
}

export function resendRejectedHandler(status: number, code: string) {
  return http.post('*/api/admin/invitations/:id/resend', () => problem(status, code))
}

function problem(status: number, code: string) {
  return HttpResponse.json(
    { status, title: code, code },
    { status, headers: { 'content-type': 'application/problem+json' } },
  )
}
