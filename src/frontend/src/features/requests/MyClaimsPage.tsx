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
import { useWithdrawClaim, useMyClaims } from './api'
import { releaseFailure } from './releaseFailure'

/** "My Claims": the Requests the Member took as Sitter. They see the exact address once it is theirs (spec §4.6). */
export function MyClaimsPage() {
  const intl = useIntl()
  const claims = useMyClaims()
  const release = useWithdrawClaim()

  if (claims.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>
          <FormattedMessage id="requests.claims.loadFailed.title" />
        </AlertTitle>
        <AlertDescription>
          <FormattedMessage id="requests.claims.loadFailed.body" />
        </AlertDescription>
      </Alert>
    )
  }
  if (!claims.data) return null

  const items = claims.data.pages.flatMap((page) => page.items)
  if (items.length === 0)
    return (
      <p>
        <FormattedMessage id="requests.claims.empty" />
      </p>
    )

  return (
    <div className="space-y-4">
      {release.isError && (
        <Alert variant="destructive">
          <AlertTitle>
            <FormattedMessage id="requests.claims.withdrawFailed.title" />
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
              <FormattedMessage id="requests.claims.starts" />
            </TableHead>
            <TableHead>
              <FormattedMessage id="requests.claims.areaAndAddress" />
            </TableHead>
            <TableHead>
              <FormattedMessage id="requests.claims.state" />
            </TableHead>
            <TableHead className="text-right">
              <FormattedMessage id="requests.claims.credits" />
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
                {request.status === 'Claimed' && (
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={release.isPending}
                    aria-label={intl.formatMessage(
                      { id: 'requests.claims.withdrawAria' },
                      { area: request.area, starts: formatInstant(request.startsAt) },
                    )}
                    onClick={() => {
                      release.mutate(request.id)
                    }}
                  >
                    <FormattedMessage id="requests.claims.withdraw" />
                  </Button>
                )}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      {claims.hasNextPage && (
        <Button
          variant="outline"
          onClick={() => void claims.fetchNextPage()}
          disabled={claims.isFetchingNextPage}
        >
          <FormattedMessage id="requests.claims.loadMore" />
        </Button>
      )}
    </div>
  )
}
