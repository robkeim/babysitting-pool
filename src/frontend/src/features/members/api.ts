import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '@/lib/api/client'
import type { AdminMemberPage, OffboardMemberResult } from './types'

export const memberKeys = {
  all: () => ['admin', 'members'] as const,
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
