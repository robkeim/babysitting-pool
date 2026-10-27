import { HeartHandshake } from 'lucide-react'
import { FormattedMessage, useIntl } from 'react-intl'
import { EmptyState } from '@/components/EmptyState'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { formatInstant } from '@/features/credits'
import { useWithdrawClaim, useMyClaims } from './api'
import { releaseFailure } from './releaseFailure'
import { RequestCard } from './RequestCard'

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
      <EmptyState icon={HeartHandshake}>
        <FormattedMessage id="requests.claims.empty" />
      </EmptyState>
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
      <ul className="space-y-3">
        {items.map((request) => (
          <li key={request.id}>
            <RequestCard
              {...request}
              action={
                request.status === 'Claimed' && (
                  <Button
                    variant="outline"
                    className="h-11 lg:h-8"
                    disabled={release.isPending}
                    aria-label={intl.formatMessage(
                      { id: 'requests.claims.withdrawAria' },
                      {
                        area:
                          request.area ??
                          intl.formatMessage({ id: 'requests.post.location.SittersHouse' }),
                        starts: formatInstant(request.startsAt),
                      },
                    )}
                    onClick={() => {
                      release.mutate(request.id)
                    }}
                  >
                    <FormattedMessage id="requests.claims.withdraw" />
                  </Button>
                )
              }
            />
          </li>
        ))}
      </ul>
      {claims.hasNextPage && (
        <Button
          variant="outline"
          className="h-11 w-full lg:h-8 lg:w-auto"
          onClick={() => void claims.fetchNextPage()}
          disabled={claims.isFetchingNextPage}
        >
          <FormattedMessage id="requests.claims.loadMore" />
        </Button>
      )}
    </div>
  )
}
