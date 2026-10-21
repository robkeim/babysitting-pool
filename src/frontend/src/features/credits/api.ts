import { useInfiniteQuery } from '@tanstack/react-query'
import { apiFetch } from '@/lib/api/client'
import type { AccountView } from './types'

export const creditsKeys = {
  account: () => ['credits', 'account'] as const,
}

/** The Member's Credit Account; each page carries the balances and the next slice of the Ledger. */
export function useMyAccount() {
  return useInfiniteQuery({
    queryKey: creditsKeys.account(),
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) =>
      apiFetch<AccountView>(
        pageParam ? `/api/me/account?cursor=${encodeURIComponent(pageParam)}` : '/api/me/account',
      ),
    getNextPageParam: (lastPage) => lastPage.ledger.nextCursor ?? undefined,
  })
}
