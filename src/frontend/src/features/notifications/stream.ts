import { type InfiniteData, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { requestsKeys } from '@/features/requests'
import { notificationKeys } from './api'
import type { NotificationPage, NotificationView } from './types'

export const STREAM_PATH = '/api/notifications/stream'

type Cache = InfiniteData<NotificationPage, string | undefined>

function parse(event: Event): unknown {
  try {
    return JSON.parse((event as MessageEvent<string>).data)
  } catch {
    return undefined
  }
}

/**
 * Keeps the Notification list live from the server's event stream (ADR 0019): a new one is added to the cache and
 * handed to `onCreated`, a dismissal in any tab or device is mirrored, and after a reconnect the list is replaced
 * from `GET /api/notifications` because the stream replays nothing. Open it only while signed in and the flag is on.
 */
export function useNotificationStream(onCreated: (notification: NotificationView) => void) {
  const client = useQueryClient()
  const created = useRef(onCreated)
  useEffect(() => {
    created.current = onCreated
  })

  useEffect(() => {
    const source = new EventSource(STREAM_PATH)
    let dropped = false

    const edit = (update: (data: Cache) => Cache) => {
      client.setQueryData<Cache>(notificationKeys.all(), (data) => (data ? update(data) : data))
    }

    source.addEventListener('open', () => {
      if (!dropped) return
      dropped = false
      void client.invalidateQueries({ queryKey: notificationKeys.all() })
    })
    source.addEventListener('error', () => {
      dropped = true
    })
    source.addEventListener('notification.created', (event) => {
      const notification = parse(event) as NotificationView | undefined
      if (!notification) return
      edit((data) => {
        if (data.pages.some((page) => page.items.some((item) => item.id === notification.id)))
          return data
        const [first, ...rest] = data.pages
        return first
          ? { ...data, pages: [{ ...first, items: [notification, ...first.items] }, ...rest] }
          : data
      })
      // What the row says about its Request is derived from these lists (ADR 0019 amendment, #154).
      void client.invalidateQueries({ queryKey: requestsKeys.open() })
      void client.invalidateQueries({ queryKey: requestsKeys.mine() })
      created.current(notification)
    })
    source.addEventListener('notification.dismissed', (event) => {
      const dismissed = parse(event) as { id: string } | undefined
      if (!dismissed) return
      edit((data) => ({
        ...data,
        pages: data.pages.map((page) => ({
          ...page,
          items: page.items.filter((item) => item.id !== dismissed.id),
        })),
      }))
    })
    source.addEventListener('notifications.cleared', () => {
      edit((data) => ({
        ...data,
        pages: data.pages.map((page) => ({ ...page, items: [], nextCursor: null })),
      }))
    })

    return () => {
      source.close()
    }
  }, [client])
}
