import type { components } from '@/lib/api/schema.gen'

export type NotificationView = components['schemas']['NotificationView']
export type NotificationPage = components['schemas']['PageOfNotificationView']

/** The `kind` values the API carries (ADR 0019). */
export const NOTIFICATION_KINDS = {
  requestPosted: 'request_posted',
  requestClaimed: 'request_claimed',
} as const
