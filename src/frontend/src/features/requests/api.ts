import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { creditsKeys } from '@/features/credits'
import { apiFetch } from '@/lib/api/client'
import type { CostQuote, PostRequestBody, QuoteDetails, RequestPage, RequestView } from './types'

export const requestsKeys = {
  mine: () => ['requests', 'mine'] as const,
  quote: (details: QuoteDetails) => ['requests', 'quote', details] as const,
}

/** The Member's own Requests, newest posted first. */
export function useMyRequests() {
  return useInfiniteQuery({
    queryKey: requestsKeys.mine(),
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) =>
      apiFetch<RequestPage>(
        pageParam ? `/api/me/requests?cursor=${encodeURIComponent(pageParam)}` : '/api/me/requests',
      ),
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  })
}

/** The calculator's cost for the details as entered, so the preview never re-implements spec §3. */
export function useQuote(details: QuoteDetails, enabled: boolean) {
  return useQuery({
    queryKey: requestsKeys.quote(details),
    queryFn: () => {
      const params = new URLSearchParams({
        durationMinutes: String(details.durationMinutes),
        childCount: String(details.childCount),
        location: details.location,
        transport: details.transport,
        lunchOrDinner: String(details.lunchOrDinner),
        snack: String(details.snack),
      })
      return apiFetch<CostQuote>(`/api/requests/quote?${params.toString()}`)
    },
    enabled,
    retry: false,
  })
}

/** Posts a Request, which takes its cost into Escrow: the list and the balance are both stale afterwards. */
export function usePostRequest() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: PostRequestBody) =>
      apiFetch<RequestView>('/api/requests', { method: 'POST', body }),
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: requestsKeys.mine() }),
        client.invalidateQueries({ queryKey: creditsKeys.account() }),
      ])
    },
  })
}
