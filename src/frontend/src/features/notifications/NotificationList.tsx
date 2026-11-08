import { Bell, Loader2, X } from 'lucide-react'
import { useIntl } from 'react-intl'
import { Button } from '@/components/ui/button'
import { formatInstant } from '@/features/credits'
import { useMyRequests, useOpenRequests } from '@/features/requests'
import { type MessageId } from '@/lib/i18n'
import { useDismissAllNotifications, useDismissNotification, useNotifications } from './api'
import { NOTIFICATION_KINDS, type NotificationView } from './types'

/**
 * The panel's content: the Member's Notifications newest first, each shown with its Request's current state. The
 * state comes from the lists the app already has: the open list for a Posted Request, My Requests for a Claimed
 * one (ADR 0019 stores ids only). A list that has more pages than loaded proves nothing, so a row is only stale
 * once the whole open list is in and the Request is not on it.
 */
export function NotificationList() {
  const intl = useIntl()
  const notifications = useNotifications()
  const dismiss = useDismissNotification()
  const dismissAll = useDismissAllNotifications()
  const open = useOpenRequests()
  const mine = useMyRequests()

  if (notifications.isError) {
    return (
      <p role="alert" className="p-4 text-destructive">
        {intl.formatMessage({ id: 'notifications.panel.loadFailed' })}
      </p>
    )
  }
  if (!notifications.data) {
    return (
      <p role="status" className="flex items-center justify-center gap-2 p-8 text-muted-foreground">
        <Loader2 className="size-4 animate-spin" aria-hidden />
        {intl.formatMessage({ id: 'notifications.panel.loading' })}
      </p>
    )
  }

  const items = notifications.data.pages.flatMap((page) => page.items)
  if (items.length === 0) {
    return (
      <div className="flex flex-col items-center gap-3 p-8 text-center text-muted-foreground">
        <Bell className="size-8" aria-hidden />
        <p>{intl.formatMessage({ id: 'notifications.panel.empty' })}</p>
      </div>
    )
  }

  const openIds =
    open.data && !open.hasNextPage
      ? new Set(open.data.pages.flatMap((page) => page.items.map((request) => request.id)))
      : undefined
  const myRequests = mine.data?.pages.flatMap((page) => page.items) ?? []

  function describe(notification: NotificationView): {
    text: string
    stale: boolean
    href: string
  } {
    if (notification.kind === NOTIFICATION_KINDS.requestPosted) {
      const date = notification.startsAt ? formatInstant(notification.startsAt) : ''
      return {
        text: notification.area
          ? intl.formatMessage(
              { id: 'notifications.posted.withArea' },
              { area: notification.area, date },
            )
          : intl.formatMessage({ id: 'notifications.posted.noArea' }, { date }),
        stale: openIds !== undefined && !openIds.has(notification.requestId),
        href: '/open-requests',
      }
    }
    if (notification.kind === NOTIFICATION_KINDS.requestCanceled) {
      const date = notification.startsAt ? formatInstant(notification.startsAt) : ''
      return {
        text: intl.formatMessage(
          { id: date ? 'notifications.canceled.dated' : 'notifications.canceled.generic' },
          { date },
        ),
        stale: false,
        href: '/claims',
      }
    }
    if (notification.kind === NOTIFICATION_KINDS.requestWithdrawn) {
      const date = notification.startsAt ? formatInstant(notification.startsAt) : ''
      return {
        text: intl.formatMessage(
          { id: date ? 'notifications.withdrawn.dated' : 'notifications.withdrawn.generic' },
          { date },
        ),
        stale: false,
        href: '/requests',
      }
    }
    const request = myRequests.find((candidate) => candidate.id === notification.requestId)
    const date = request ? formatInstant(request.startsAt) : ''
    const id: MessageId = !request
      ? 'notifications.claimed.generic'
      : request.sitterDisplayName
        ? 'notifications.claimed.named'
        : 'notifications.claimed.unnamed'
    return {
      text: intl.formatMessage({ id }, { sitter: request?.sitterDisplayName, date }),
      stale: false,
      href: '/requests',
    }
  }

  return (
    <>
      <ul className="min-h-0 divide-y overflow-y-auto">
        {items.map((notification) => {
          const { text, stale, href } = describe(notification)
          return (
            <li key={notification.id} className="flex items-start gap-1 py-1 pr-1 pl-4">
              <div className="min-w-0 flex-1 py-2.5 [overflow-wrap:anywhere]">
                {stale ? (
                  <p className="text-muted-foreground">{text}</p>
                ) : (
                  <a href={href} className="hover:underline">
                    {text}
                  </a>
                )}
                {stale && (
                  <p className="mt-0.5 text-xs font-medium text-muted-foreground">
                    {intl.formatMessage({ id: 'notifications.row.unavailable' })}
                  </p>
                )}
              </div>
              <Button
                variant="ghost"
                size="icon-lg"
                aria-label={intl.formatMessage(
                  { id: 'notifications.row.dismiss' },
                  { summary: text },
                )}
                disabled={dismiss.isPending}
                onClick={() => {
                  dismiss.mutate(notification.id)
                }}
              >
                <X />
              </Button>
            </li>
          )
        })}
      </ul>
      <div className="flex flex-wrap items-center justify-between gap-2 border-t p-2">
        {notifications.hasNextPage ? (
          <Button
            variant="ghost"
            disabled={notifications.isFetchingNextPage}
            onClick={() => void notifications.fetchNextPage()}
          >
            {intl.formatMessage({ id: 'notifications.panel.loadMore' })}
          </Button>
        ) : (
          <span />
        )}
        <Button
          variant="ghost"
          disabled={dismissAll.isPending}
          onClick={() => {
            dismissAll.mutate()
          }}
        >
          {intl.formatMessage({ id: 'notifications.panel.dismissAll' })}
        </Button>
      </div>
      {(dismiss.isError || dismissAll.isError) && (
        <p role="alert" className="border-t p-3 text-destructive">
          {intl.formatMessage({ id: 'common.somethingWentWrong' })}
        </p>
      )}
    </>
  )
}
