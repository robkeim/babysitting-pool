import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { apiFetch } from '@/lib/api/client'
import type {
  DormantMemberPage,
  MemberDetailView,
  MemberPage,
  MemberRequestPage,
  PoolOverview,
} from './types'

export const memberKeys = {
  all: () => ['admin', 'members'] as const,
  detail: (id: string) => ['admin', 'members', id] as const,
  requests: (id: string) => ['admin', 'members', id, 'requests'] as const,
  pool: () => ['admin', 'pool-overview'] as const,
  dormant: () => ['admin', 'dormant-members'] as const,
}

function withCursor(path: string, cursor: string | undefined) {
  return cursor ? `${path}?cursor=${encodeURIComponent(cursor)}` : path
}

/** Every Member, closed ones included; Admin only, read-only (spec §2A). */
export function useMembers() {
  return useInfiniteQuery({
    queryKey: memberKeys.all(),
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) => apiFetch<MemberPage>(withCursor('/api/admin/members', pageParam)),
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  })
}

/** One Member's balances; each page also carries the next slice of their Ledger. */
export function useMemberDetail(id: string) {
  return useInfiniteQuery({
    queryKey: memberKeys.detail(id),
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) =>
      apiFetch<MemberDetailView>(withCursor(`/api/admin/members/${id}`, pageParam)),
    getNextPageParam: (lastPage) => lastPage.ledger.nextCursor ?? undefined,
  })
}

export function useMemberRequests(id: string) {
  return useInfiniteQuery({
    queryKey: memberKeys.requests(id),
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) =>
      apiFetch<MemberRequestPage>(withCursor(`/api/admin/members/${id}/requests`, pageParam)),
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  })
}

export function usePoolOverview() {
  return useQuery({
    queryKey: memberKeys.pool(),
    queryFn: () => apiFetch<PoolOverview>('/api/admin/pool-overview'),
  })
}

/** Members with no activity past the dormancy threshold, longest dormant first (spec §5). */
export function useDormantMembers() {
  return useQuery({
    queryKey: memberKeys.dormant(),
    queryFn: () => apiFetch<DormantMemberPage>('/api/admin/dormant-members'),
  })
}
