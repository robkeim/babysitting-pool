import { http, HttpResponse } from 'msw'
import type { NotificationPage, NotificationView } from './types'

export const postedNotification: NotificationView = {
  id: '5b0c1c3e-5d8a-4a38-9b1d-0a7a1c1b9e01',
  kind: 'request_posted',
  createdAt: '2026-03-01T09:00:00Z',
  requestId: '9d1f2c3e-5d8a-4a38-9b1d-0a7a1c1b9e22',
  area: 'Riverside',
  startsAt: '2026-03-02T10:00:00Z',
  sitterId: null,
}

export const claimedNotification: NotificationView = {
  id: '5b0c1c3e-5d8a-4a38-9b1d-0a7a1c1b9e02',
  kind: 'request_claimed',
  createdAt: '2026-03-01T10:00:00Z',
  requestId: '2f6c1c3e-5d8a-4a38-9b1d-0a7a1c1b9e11',
  area: null,
  startsAt: null,
  sitterId: '7a7a1c3e-5d8a-4a38-9b1d-0a7a1c1b9e33',
}

export const canceledNotification: NotificationView = {
  id: '5b0c1c3e-5d8a-4a38-9b1d-0a7a1c1b9e03',
  kind: 'request_canceled',
  createdAt: '2026-03-01T11:00:00Z',
  requestId: '2f6c1c3e-5d8a-4a38-9b1d-0a7a1c1b9e44',
  area: null,
  startsAt: '2026-03-02T10:00:00Z',
  sitterId: null,
}

export const withdrawnNotification: NotificationView = {
  id: '5b0c1c3e-5d8a-4a38-9b1d-0a7a1c1b9e04',
  kind: 'request_withdrawn',
  createdAt: '2026-03-01T11:30:00Z',
  requestId: '2f6c1c3e-5d8a-4a38-9b1d-0a7a1c1b9e55',
  area: null,
  startsAt: '2026-03-02T10:00:00Z',
  sitterId: null,
}

export function notificationsHandler(items: NotificationView[], nextCursor: string | null = null) {
  return http.get('*/api/notifications', () =>
    HttpResponse.json({ items, nextCursor } satisfies NotificationPage),
  )
}

export function notificationsFailureHandler() {
  return http.get('*/api/notifications', () =>
    HttpResponse.json(
      { status: 500, title: 'Boom', code: 'internal_error' },
      { status: 500, headers: { 'content-type': 'application/problem+json' } },
    ),
  )
}

/** Calls `onDismiss` with the id, then answers 204. */
export function dismissNotificationHandler(onDismiss: (id: string) => void) {
  return http.delete('*/api/notifications/:id', ({ params }) => {
    onDismiss(String(params.id))
    return new HttpResponse(null, { status: 204 })
  })
}

export function dismissAllNotificationsHandler(onDismissAll: () => void) {
  return http.delete('*/api/notifications', () => {
    onDismissAll()
    return new HttpResponse(null, { status: 204 })
  })
}

/** The open list as the panel reads it to tell whether a Posted Request can still be claimed: only ids matter. */
export function openRequestIdsHandler(ids: string[], nextCursor: string | null = null) {
  return http.get('*/api/requests/open', () =>
    HttpResponse.json({ items: ids.map((id) => ({ id })), nextCursor }),
  )
}

/** My Requests as the panel reads them to name a Claimed Request's Sitter and date. */
export function claimedRequestsHandler(
  requests: { id: string; startsAt: string; sitterDisplayName: string | null }[],
) {
  return http.get('*/api/me/requests', () =>
    HttpResponse.json({
      items: requests.map((request) => ({ ...request, status: 'Claimed' })),
      nextCursor: null,
    }),
  )
}
