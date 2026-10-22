import { FormattedMessage, useIntl } from 'react-intl'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
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
        <p>
          <FormattedMessage id="requests.open.empty" />
        </p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>
                <FormattedMessage id="requests.open.starts" />
              </TableHead>
              <TableHead>
                <FormattedMessage id="requests.open.area" />
              </TableHead>
              <TableHead>
                <FormattedMessage id="requests.open.details" />
              </TableHead>
              <TableHead className="text-right">
                <FormattedMessage id="requests.open.credits" />
              </TableHead>
              <TableHead />
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((request) => (
              <TableRow key={request.id}>
                <TableCell>{formatInstant(request.startsAt)}</TableCell>
                <TableCell>{request.area}</TableCell>
                <TableCell>
                  <FormattedMessage
                    id="requests.details"
                    values={{ minutes: request.durationMinutes, children: request.childCount }}
                  />
                </TableCell>
                <TableCell className="text-right">
                  {formatCredits(request.costQuarterCredits)}
                </TableCell>
                <TableCell className="text-right">
                  <Button
                    size="sm"
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
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
      {requests.hasNextPage && (
        <Button
          variant="outline"
          onClick={() => void requests.fetchNextPage()}
          disabled={requests.isFetchingNextPage}
        >
          <FormattedMessage id="requests.open.loadMore" />
        </Button>
      )}
    </div>
  )
}
