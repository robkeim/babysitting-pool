import { useQuery } from '@tanstack/react-query'
import { apiFetch } from '@/lib/api/client'
import { useFeatureFlagOverrides } from './overrides'
import type { FeatureFlags } from './types'

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

/** Whether the flag is on for the caller. Off while the flags load, fail to load, or the name is not declared. */
export function useFeatureFlag(name: string): boolean {
  const overrides = useFeatureFlagOverrides()
  const flags = useFeatureFlags(overrides === undefined)
  return (overrides ?? flags.data)?.[name] ?? false
}
