import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '@/lib/api/client'
import type {
  AdminMemberPage,
  DormantMemberPage,
  MemberDetailView,
  MemberRequestPage,
  OffboardMemberResult,
  PoolOverview,
  SettleResult,
} from './types'

export const memberKeys = {
  all: () => ['admin', 'members'] as const,
  detail: (id: string) => ['admin', 'members', id] as const,
  requests: (id: string) => ['admin', 'members', id, 'requests'] as const,
  pool: () => ['admin', 'pool-overview'] as const,
  dormant: () => ['admin', 'dormant-members'] as const,
}

/** Every Member, open and closed; Admin only (spec §2A). */
export function useMembers() {
  return useQuery({
    queryKey: memberKeys.all(),
    queryFn: () => apiFetch<AdminMemberPage>('/api/admin/members'),
  })
}

/** Closes the account, releases the Member's open Requests and burns their balance; safe to repeat. */
export function useOffboardMember() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<OffboardMemberResult>(`/api/admin/members/${id}/offboard`, { method: 'POST' }),
    onSettled: () => client.invalidateQueries({ queryKey: memberKeys.all() }),
  })
}

/** Replaces a closed Member's name and email with "Former member"; irreversible (ADR 0014). */
export function useEraseMember() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<undefined>(`/api/admin/members/${id}/erase`, { method: 'POST' }),
    onSettled: () => client.invalidateQueries({ queryKey: memberKeys.all() }),
  })
}

function withCursor(path: string, cursor: string | undefined) {
  return cursor ? `${path}?cursor=${encodeURIComponent(cursor)}` : path
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

/** Runs Settle now: expiry, payout and demurrage that are due; idempotent, so safe to repeat (ADR 0007). */
export function useRunSettle() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => apiFetch<SettleResult>('/api/admin/settle', { method: 'POST' }),
    onSettled: () =>
      Promise.all([
        client.invalidateQueries({ queryKey: memberKeys.pool() }),
        client.invalidateQueries({ queryKey: memberKeys.dormant() }),
      ]),
  })
}
