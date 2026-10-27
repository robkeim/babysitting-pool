import { HandHeart } from 'lucide-react'
import { FormattedMessage, useIntl } from 'react-intl'
import { EmptyState } from '@/components/EmptyState'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { formatInstant } from '@/features/credits'
import { ApiError } from '@/lib/api/client'
import { type MessageId } from '@/lib/i18n'
import { useClaimRequest, useOpenRequests } from './api'
import { RequestCard } from './RequestCard'

const CLAIM_FAILURE_CODES: Partial<Record<string, MessageId>> = {
  invalid_state: 'error.invalid_state',
  own_request: 'error.own_request',
  claim_cutoff_passed: 'error.claim_cutoff_passed',
  sitter_overlap: 'error.sitter_overlap',
  request_not_found: 'error.request_not_found',
  concurrent_update: 'error.invalid_state',
}

function claimFailure(error: unknown): MessageId {
  const code = error instanceof ApiError ? error.code : ''
  return CLAIM_FAILURE_CODES[code] ?? 'common.somethingWentWrong'
}

/** "Open Requests": what other Members still need, with the rough area only (spec §4.4, §4.6). */
export function OpenRequestsPage() {
  const intl = useIntl()
  const requests = useOpenRequests()
  const claim = useClaimRequest()

  if (requests.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>
          <FormattedMessage id="requests.open.loadFailed.title" />
        </AlertTitle>
        <AlertDescription>
          <FormattedMessage id="requests.open.loadFailed.body" />
        </AlertDescription>
      </Alert>
    )
  }
  if (!requests.data) return null

  const items = requests.data.pages.flatMap((page) => page.items)

  return (
    <div className="space-y-4">
      {claim.isError && (
        <Alert variant="destructive">
          <AlertTitle>
            <FormattedMessage id="requests.open.claimFailed.title" />
          </AlertTitle>
          <AlertDescription>
            <FormattedMessage id={claimFailure(claim.error)} />
          </AlertDescription>
        </Alert>
      )}
      {items.length === 0 ? (
        <EmptyState icon={HandHeart}>
          <FormattedMessage id="requests.open.empty" />
        </EmptyState>
      ) : (
        <ul className="space-y-3">
          {items.map((request) => (
            <li key={request.id}>
              <RequestCard
                {...request}
                action={
                  <Button
                    className="h-11 lg:h-8"
                    disabled={claim.isPending}
                    aria-label={intl.formatMessage(
                      { id: 'requests.open.claimAria' },
                      {
                        area:
                          request.area ??
                          intl.formatMessage({ id: 'requests.post.location.SittersHouse' }),
                        starts: formatInstant(request.startsAt),
                      },
                    )}
                    onClick={() => {
                      claim.mutate(request.id)
                    }}
                  >
                    <FormattedMessage id="requests.open.claim" />
                  </Button>
                }
              />
            </li>
          ))}
        </ul>
      )}
      {requests.hasNextPage && (
        <Button
          variant="outline"
          className="h-11 w-full lg:h-8 lg:w-auto"
          onClick={() => void requests.fetchNextPage()}
          disabled={requests.isFetchingNextPage}
        >
          <FormattedMessage id="requests.open.loadMore" />
        </Button>
      )}
    </div>
  )
}
