import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { creditsKeys } from '@/features/credits'
import { apiFetch } from '@/lib/api/client'
import type {
  CostQuote,
  OpenRequestPage,
  PostRequestBody,
  QuoteDetails,
  RequestPage,
  RequestView,
} from './types'

export const requestsKeys = {
  mine: () => ['requests', 'mine'] as const,
  open: () => ['requests', 'open'] as const,
  claims: () => ['requests', 'claims'] as const,
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

function pagedUrl(path: string, cursor: string | undefined): string {
  return cursor ? `${path}?cursor=${encodeURIComponent(cursor)}` : path
}

/** Posted Requests other Members can still claim, earliest start first. */
export function useOpenRequests() {
  return useInfiniteQuery({
    queryKey: requestsKeys.open(),
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) =>
      apiFetch<OpenRequestPage>(pagedUrl('/api/requests/open', pageParam)),
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  })
}

/** The Requests the Member claimed as Sitter, latest start first. */
export function useMyClaims() {
  return useInfiniteQuery({
    queryKey: requestsKeys.claims(),
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) => apiFetch<RequestPage>(pagedUrl('/api/me/claims', pageParam)),
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  })
}

/**
 * Claims a Request; the first Sitter wins. Whatever the answer, the open list is stale afterwards: a lost race
 * means the Request is gone, a win means it moved to My Claims.
 */
export function useClaimRequest() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (requestId: string) =>
      apiFetch<RequestView>(`/api/requests/${requestId}/claim`, { method: 'POST' }),
    onSettled: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: requestsKeys.open() }),
        client.invalidateQueries({ queryKey: requestsKeys.claims() }),
      ])
    },
  })
}

/** Both Cancel and Withdraw move credits or reopen a Request, so every list and the balance are stale afterwards. */
function useReleaseRequest(action: 'cancel' | 'withdraw') {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (requestId: string) =>
      apiFetch<RequestView>(`/api/requests/${requestId}/${action}`, { method: 'POST' }),
    onSettled: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: requestsKeys.mine() }),
        client.invalidateQueries({ queryKey: requestsKeys.open() }),
        client.invalidateQueries({ queryKey: requestsKeys.claims() }),
        client.invalidateQueries({ queryKey: creditsKeys.account() }),
      ])
    },
  })
}

/** The Requester cancels a Posted or Claimed Request before its start time and is refunded in full (spec §4.5). */
export function useCancelRequest() {
  return useReleaseRequest('cancel')
}

/** The Sitter drops their claim before the start time (spec §4.5). */
export function useWithdrawClaim() {
  return useReleaseRequest('withdraw')
}
