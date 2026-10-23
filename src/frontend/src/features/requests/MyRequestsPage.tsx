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
import { useCancelRequest, useMyRequests } from './api'
import { releaseFailure } from './releaseFailure'

/** "My Requests": everything the Member posted, with its state. Only the owner sees the exact address. */
export function MyRequestsPage() {
  const intl = useIntl()
  const requests = useMyRequests()
  const release = useCancelRequest()

  if (requests.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>
          <FormattedMessage id="requests.myRequests.loadFailed.title" />
        </AlertTitle>
        <AlertDescription>
          <FormattedMessage id="requests.myRequests.loadFailed.body" />
        </AlertDescription>
      </Alert>
    )
  }
  if (!requests.data) return null

  const items = requests.data.pages.flatMap((page) => page.items)
  if (items.length === 0)
    return (
      <p>
        <FormattedMessage id="requests.myRequests.empty" />
      </p>
    )

  return (
    <div className="space-y-4">
      {release.isError && (
        <Alert variant="destructive">
          <AlertTitle>
            <FormattedMessage id="requests.myRequests.cancelFailed.title" />
          </AlertTitle>
          <AlertDescription>
            <FormattedMessage id={releaseFailure(release.error)} />
          </AlertDescription>
        </Alert>
      )}
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>
              <FormattedMessage id="requests.myRequests.starts" />
            </TableHead>
            <TableHead>
              <FormattedMessage id="requests.myRequests.areaAndAddress" />
            </TableHead>
            <TableHead>
              <FormattedMessage id="requests.myRequests.state" />
            </TableHead>
            <TableHead className="text-right">
              <FormattedMessage id="requests.myRequests.credits" />
            </TableHead>
            <TableHead />
          </TableRow>
        </TableHeader>
        <TableBody>
          {items.map((request) => (
            <TableRow key={request.id}>
              <TableCell>{formatInstant(request.startsAt)}</TableCell>
              <TableCell>
                {request.area}
                <br />
                <span className="text-muted-foreground">{request.address}</span>
              </TableCell>
              <TableCell>
                <FormattedMessage id={`requests.status.${request.status}`} />
              </TableCell>
              <TableCell className="text-right">
                {formatCredits(request.costQuarterCredits)}
              </TableCell>
              <TableCell className="text-right">
                {(request.status === 'Posted' || request.status === 'Claimed') && (
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={release.isPending}
                    aria-label={intl.formatMessage(
                      { id: 'requests.myRequests.cancelAria' },
                      { area: request.area, starts: formatInstant(request.startsAt) },
                    )}
                    onClick={() => {
                      release.mutate(request.id)
                    }}
                  >
                    <FormattedMessage id="requests.myRequests.cancel" />
                  </Button>
                )}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      {requests.hasNextPage && (
        <Button
          variant="outline"
          onClick={() => void requests.fetchNextPage()}
          disabled={requests.isFetchingNextPage}
        >
          <FormattedMessage id="requests.myRequests.loadMore" />
        </Button>
      )}
    </div>
  )
}
