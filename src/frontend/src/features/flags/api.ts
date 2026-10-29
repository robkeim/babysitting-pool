import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '@/lib/api/client'
import { useFeatureFlagOverrides } from './overrides'
import type { FeatureFlags, FeatureFlagView, Page, UpdateFeatureFlagRequest } from './types'

export const flagKeys = {
  all: () => ['feature-flags'] as const,
}

const REFRESH_MS = 60_000

/** The server resolves targeting for the caller; the browser only reads the answer (ADR 0016). */
export function useFeatureFlags(enabled = true) {
  return useQuery({
    queryKey: flagKeys.all(),
    queryFn: () => apiFetch<FeatureFlags>('/api/feature-flags'),
    enabled,
    refetchInterval: REFRESH_MS,
    refetchOnWindowFocus: true,
    staleTime: REFRESH_MS,
  })
}

/** The flag's value for the caller, or undefined while the flags load or fail to load. */
export function useFeatureFlagValue(name: string): boolean | undefined {
  const overrides = useFeatureFlagOverrides()
  const flags = useFeatureFlags(overrides === undefined)
  const resolved = overrides ?? flags.data
  return resolved === undefined ? undefined : (resolved[name] ?? false)
}

/** Whether the flag is on for the caller. Off while the flags load, fail to load, or the name is not declared. */
export function useFeatureFlag(name: string): boolean {
  return useFeatureFlagValue(name) ?? false
}

export const adminFlagKeys = {
  all: () => ['admin', 'feature-flags'] as const,
}

/** Every declared flag with its stored settings; Admin only (ADR 0016). */
export function useAdminFeatureFlags() {
  return useQuery({
    queryKey: adminFlagKeys.all(),
    queryFn: () => apiFetch<Page<FeatureFlagView>>('/api/admin/feature-flags'),
  })
}

/** Replaces one flag's settings whole; the caller's own resolved flags are refetched. */
export function useUpdateFeatureFlag() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (request: UpdateFeatureFlagRequest) =>
      apiFetch<FeatureFlagView>('/api/admin/feature-flags', { method: 'PUT', body: request }),
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: adminFlagKeys.all() }),
        client.invalidateQueries({ queryKey: flagKeys.all() }),
      ])
    },
  })
}
