import { useEffect, useRef } from 'react'
import { useIntl } from 'react-intl'
import { formatInstant } from '@/features/credits'
import { NOTIFICATION_KINDS, type NotificationView } from './types'

const TOAST_MS = 6000

/** A transient announcement of a Notification that just arrived; the bell keeps it until dismissed. */
export function NotificationToast({
  notification,
  onDone,
}: {
  notification: NotificationView
  onDone: () => void
}) {
  const intl = useIntl()
  const done = useRef(onDone)
  useEffect(() => {
    done.current = onDone
  })
  useEffect(() => {
    const timer = setTimeout(() => {
      done.current()
    }, TOAST_MS)
    return () => {
      clearTimeout(timer)
    }
  }, [])

  const date = notification.startsAt ? formatInstant(notification.startsAt) : ''
  const text =
    notification.kind === NOTIFICATION_KINDS.requestPosted
      ? notification.area
        ? intl.formatMessage(
            { id: 'notifications.posted.withArea' },
            { area: notification.area, date },
          )
        : intl.formatMessage({ id: 'notifications.posted.noArea' }, { date })
      : notification.kind === NOTIFICATION_KINDS.requestCancelled
        ? intl.formatMessage({ id: 'notifications.cancelled.generic' })
        : intl.formatMessage({ id: 'notifications.claimed.generic' })

  return (
    <div
      role="status"
      className="fixed inset-x-4 top-16 z-30 rounded-lg border bg-card p-4 text-sm shadow-lg lg:right-4 lg:left-auto lg:w-80"
    >
      {text}
    </div>
  )
}
