import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '@/lib/api/client'
import type { ProfileView } from './types'

export const profileKeys = {
  mine: () => ['profile', 'mine'] as const,
}

/** The Member's own area and address (spec §2A). */
export function useMyProfile() {
  return useQuery({
    queryKey: profileKeys.mine(),
    queryFn: () => apiFetch<ProfileView>('/api/me/profile'),
  })
}

/** Saves the area and address that every Request at the Member's house copies. */
export function useUpdateProfile() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: { area: string; address: string }) =>
      apiFetch<ProfileView>('/api/me/profile', { method: 'PUT', body }),
    onSuccess: (profile) => {
      client.setQueryData(profileKeys.mine(), profile)
    },
  })
}
