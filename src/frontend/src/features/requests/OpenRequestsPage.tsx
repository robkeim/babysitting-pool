import { CalendarClock, Clock, Coins, HandHeart, MapPin } from 'lucide-react'
import { FormattedMessage, useIntl } from 'react-intl'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { formatCredits, formatInstant } from '@/features/credits'
import { ApiError } from '@/lib/api/client'
import { type MessageId } from '@/lib/i18n'
import { useClaimRequest, useOpenRequests } from './api'

const CLAIM_FAILURE_CODES: Partial<Record<string, MessageId>> = {
  invalid_state: 'error.invalid_state',
  own_request: 'error.own_request',
  claim_cutoff_passed: 'error.claim_cutoff_passed',
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
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-8 text-center text-muted-foreground">
            <HandHeart className="size-8" aria-hidden />
            <p>
              <FormattedMessage id="requests.open.empty" />
            </p>
          </CardContent>
        </Card>
      ) : (
        <ul className="space-y-3">
          {items.map((request) => (
            <li key={request.id}>
              <Card>
                <CardContent className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
                  <div className="min-w-0 space-y-2">
                    <p className="flex items-start gap-2 font-semibold">
                      <CalendarClock
                        className="mt-0.5 size-4 shrink-0 text-muted-foreground"
                        aria-hidden
                      />
                      <span className="min-w-0 [overflow-wrap:anywhere]">
                        {formatInstant(request.startsAt)}
                      </span>
                    </p>
                    <p className="flex items-start gap-2 text-muted-foreground">
                      <MapPin className="mt-0.5 size-4 shrink-0" aria-hidden />
                      <span className="min-w-0 [overflow-wrap:anywhere]">{request.area}</span>
                    </p>
                    <p className="flex items-start gap-2 text-muted-foreground">
                      <Clock className="mt-0.5 size-4 shrink-0" aria-hidden />
                      <span className="min-w-0 [overflow-wrap:anywhere]">
                        <FormattedMessage
                          id="requests.details"
                          values={{
                            minutes: request.durationMinutes,
                            children: request.childCount,
                          }}
                        />
                      </span>
                    </p>
                  </div>
                  <div className="flex flex-col gap-3 lg:items-end">
                    <p className="flex items-center gap-1.5 self-start rounded-full bg-accent px-3 py-1 font-semibold text-accent-foreground tabular-nums lg:self-auto">
                      <Coins className="size-4" aria-hidden />
                      <span className="sr-only">
                        <FormattedMessage id="requests.open.credits" />
                      </span>
                      {formatCredits(request.costQuarterCredits)}
                    </p>
                    <Button
                      className="h-11 lg:h-8"
                      disabled={claim.isPending}
                      aria-label={intl.formatMessage(
                        { id: 'requests.open.claimAria' },
                        { area: request.area, starts: formatInstant(request.startsAt) },
                      )}
                      onClick={() => {
                        claim.mutate(request.id)
                      }}
                    >
                      <FormattedMessage id="requests.open.claim" />
                    </Button>
                  </div>
                </CardContent>
              </Card>
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
