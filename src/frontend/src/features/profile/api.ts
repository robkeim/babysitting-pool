import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '@/lib/api/client'
import type { AddressSuggestion, ProfileUpdate, ProfileView } from './types'

export const profileKeys = {
  mine: () => ['profile', 'mine'] as const,
  suggestions: (query: string) => ['profile', 'address-suggestions', query] as const,
}

/** The shortest query the API answers (spec §2A). */
export const MIN_QUERY_LENGTH = 3

/** The Member's own area and address (spec §2A). */
export function useMyProfile() {
  return useQuery({
    queryKey: profileKeys.mine(),
    queryFn: () => apiFetch<ProfileView>('/api/me/profile'),
  })
}

/** French addresses matching a partly typed one (ADR 0020). An empty `query` searches nothing. */
export function useAddressSuggestions(query: string) {
  return useQuery({
    queryKey: profileKeys.suggestions(query),
    queryFn: () =>
      apiFetch<{ items: AddressSuggestion[] }>(
        `/api/address-suggestions?q=${encodeURIComponent(query)}`,
      ),
    enabled: query.length >= MIN_QUERY_LENGTH,
    staleTime: 60_000,
    retry: false,
  })
}

/** Saves the picked address and instructions that every Request at the Member's house copies. */
export function useUpdateProfile() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: ProfileUpdate) =>
      apiFetch<ProfileView>('/api/me/profile', { method: 'PUT', body }),
    onSuccess: (profile) => {
      client.setQueryData(profileKeys.mine(), profile)
    },
  })
}
