import { useQuery } from '@tanstack/react-query'
import { apiFetch } from '@/lib/api/client'
import type { RulesView } from './types'

export const rulesKeys = {
  all: () => ['rules'] as const,
}

/** The co-op rules as the engine enforces them, so the page never restates a number (spec §7). */
export function useRules() {
  return useQuery({
    queryKey: rulesKeys.all(),
    queryFn: () => apiFetch<RulesView>('/api/rules'),
  })
}
