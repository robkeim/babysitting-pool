import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '@/lib/api/client'
import type { NotificationPage } from './types'

export const notificationKeys = {
  all: () => ['notifications'] as const,
}

/** The Member's Notifications, newest first (ADR 0019). */
export function useNotifications() {
  return useInfiniteQuery({
    queryKey: notificationKeys.all(),
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) =>
      apiFetch<NotificationPage>(
        pageParam
          ? `/api/notifications?cursor=${encodeURIComponent(pageParam)}`
          : '/api/notifications',
      ),
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  })
}

/** Dismissing deletes the row; a row already gone (404) is gone either way, so the list is refetched regardless. */
export function useDismissNotification() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<undefined>(`/api/notifications/${id}`, { method: 'DELETE' }),
    onSettled: () => client.invalidateQueries({ queryKey: notificationKeys.all() }),
  })
}

export function useDismissAllNotifications() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => apiFetch<undefined>('/api/notifications', { method: 'DELETE' }),
    onSettled: () => client.invalidateQueries({ queryKey: notificationKeys.all() }),
  })
}
