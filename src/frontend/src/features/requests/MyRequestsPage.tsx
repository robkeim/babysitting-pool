import { ClipboardList } from 'lucide-react'
import { FormattedMessage, useIntl } from 'react-intl'
import { EmptyState } from '@/components/EmptyState'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { formatInstant } from '@/features/credits'
import { useCancelRequest, useMyRequests } from './api'
import { EditNotes } from './EditNotes'
import { releaseFailure } from './releaseFailure'
import { RequestCard } from './RequestCard'
import { ErrorReference } from '@/components/ErrorReference'

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
          <ErrorReference error={requests.error} />
        </AlertDescription>
      </Alert>
    )
  }
  if (!requests.data) return null

  const items = requests.data.pages.flatMap((page) => page.items)
  if (items.length === 0)
    return (
      <EmptyState icon={ClipboardList}>
        <FormattedMessage id="requests.myRequests.empty" />
      </EmptyState>
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
            <ErrorReference error={release.error} />
          </AlertDescription>
        </Alert>
      )}
      <ul className="space-y-3">
        {items.map((request) => (
          <li key={request.id}>
            <RequestCard
              {...request}
              footer={
                (request.status === 'Posted' || request.status === 'Claimed') && (
                  <EditNotes
                    key={request.notes ?? ''}
                    requestId={request.id}
                    notes={request.notes}
                    status={request.status}
                  />
                )
              }
              action={
                (request.status === 'Posted' || request.status === 'Claimed') && (
                  <Button
                    variant="outline"
                    className="h-11 lg:h-8"
                    disabled={release.isPending}
                    aria-label={intl.formatMessage(
                      { id: 'requests.myRequests.cancelAria' },
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
                    <FormattedMessage id="requests.myRequests.cancel" />
                  </Button>
                )
              }
            />
          </li>
        ))}
      </ul>
      {requests.hasNextPage && (
        <Button
          variant="outline"
          className="h-11 w-full lg:h-8 lg:w-auto"
          onClick={() => void requests.fetchNextPage()}
          disabled={requests.isFetchingNextPage}
        >
          <FormattedMessage id="requests.myRequests.loadMore" />
        </Button>
      )}
    </div>
  )
}
