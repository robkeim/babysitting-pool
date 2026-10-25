import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '@/lib/api/client'
import type { InvitationPage, InvitationView } from './types'

export const invitationKeys = {
  all: () => ['admin', 'invitations'] as const,
}

/** Every Invitation with its state; Admin only (spec §2A). */
export function useInvitations() {
  return useQuery({
    queryKey: invitationKeys.all(),
    queryFn: () => apiFetch<InvitationPage>('/api/admin/invitations'),
  })
}

/** Emails a new Member their Invitation link. */
export function useInviteMember() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (email: string) =>
      apiFetch<InvitationView>('/api/admin/invitations', { method: 'POST', body: { email } }),
    onSuccess: () => client.invalidateQueries({ queryKey: invitationKeys.all() }),
  })
}

/** A new link and a fresh validity for a pending or expired Invitation; the old link stops working. */
export function useResendInvitation() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<InvitationView>(`/api/admin/invitations/${id}/resend`, { method: 'POST' }),
    onSettled: () => client.invalidateQueries({ queryKey: invitationKeys.all() }),
  })
}
