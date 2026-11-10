import { FormattedMessage, useIntl } from 'react-intl'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatCredits, formatInstant, LedgerList } from '@/features/credits'
import { type MessageId } from '@/lib/i18n'
import { useMemberDetail, useMemberRequests } from './api'
import type { MemberRequestView } from './types'
import { ErrorReference } from '@/components/ErrorReference'

const STATUS_MESSAGES: Record<MemberRequestView['status'], MessageId> = {
  Posted: 'requests.status.Posted',
  Claimed: 'requests.status.Claimed',
  Completed: 'requests.status.Completed',
  Canceled: 'requests.status.Canceled',
  Expired: 'requests.status.Expired',
}

const ROLE_MESSAGES: Record<MemberRequestView['role'], MessageId> = {
  Requester: 'members.admin.role.Requester',
  Sitter: 'members.admin.role.Sitter',
}

function Requests({ memberId }: { memberId: string }) {
  const intl = useIntl()
  const requests = useMemberRequests(memberId)
  const items = requests.data?.pages.flatMap((page) => page.items) ?? []

  return (
    <>
      <h3 className="pt-2 text-base font-semibold">
        <FormattedMessage id="members.admin.requests.title" />
      </h3>
      {requests.isError && (
        <Alert variant="destructive">
          <AlertTitle>
            <FormattedMessage id="members.admin.loadFailed.title" />
          </AlertTitle>
        </Alert>
      )}
      {requests.data && items.length === 0 && (
        <p className="text-sm text-muted-foreground">
          <FormattedMessage id="members.admin.requests.empty" />
        </p>
      )}
      {items.length > 0 && (
        <Card>
          <CardContent>
            <ul className="divide-y">
              {items.map((request) => (
                <li key={request.id} className="space-y-1 py-3 first:pt-0 last:pb-0">
                  <p className="flex justify-between gap-3 font-medium">
                    <span>{formatInstant(request.startsAt)}</span>
                    <span className="tabular-nums">
                      {formatCredits(request.costQuarterCredits)}
                    </span>
                  </p>
                  <p className="text-sm [overflow-wrap:anywhere] text-muted-foreground">
                    {intl.formatMessage(
                      { id: 'members.admin.request.line' },
                      {
                        role: intl.formatMessage({ id: ROLE_MESSAGES[request.role] }),
                        status: intl.formatMessage({ id: STATUS_MESSAGES[request.status] }),
                        area: request.area ?? '–',
                      },
                    )}
                  </p>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
      {requests.hasNextPage && (
        <Button
          variant="outline"
          className="h-11 w-full lg:h-8 lg:w-auto"
          onClick={() => void requests.fetchNextPage()}
          disabled={requests.isFetchingNextPage}
        >
          <FormattedMessage id="credits.loadMore" />
        </Button>
      )}
    </>
  )
}

/** One Member's balances, Requests and Ledger, read-only: the Admin cannot change any of it (spec §2A). */
export function MemberDetailPage({ memberId }: { memberId: string }) {
  const intl = useIntl()
  const detail = useMemberDetail(memberId)

  if (detail.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>
          <FormattedMessage id="members.admin.detail.loadFailed.title" />
        </AlertTitle>
        <AlertDescription>
          <FormattedMessage id="members.admin.loadFailed.body" />
          <ErrorReference error={detail.error} />
        </AlertDescription>
      </Alert>
    )
  }
  const [first] = detail.data?.pages ?? []
  if (!detail.data || !first) return null

  const { member } = first
  const entries = detail.data.pages.flatMap((page) => page.ledger.items)
  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-xl font-semibold [overflow-wrap:anywhere]">{member.displayName}</h2>
        <p className="text-sm [overflow-wrap:anywhere] text-muted-foreground">{member.email}</p>
        <p className="text-sm text-muted-foreground">
          <FormattedMessage
            id="members.admin.memberSince"
            values={{ when: formatInstant(member.createdAt) }}
          />
          {member.closedAt && (
            <>
              {' · '}
              <FormattedMessage
                id="members.admin.closedAt"
                values={{ when: formatInstant(member.closedAt) }}
              />
            </>
          )}
        </p>
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <Card role="group" aria-label={intl.formatMessage({ id: 'credits.available' })}>
          <CardHeader>
            <CardTitle className="text-muted-foreground">
              <FormattedMessage id="credits.available" />
            </CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold tabular-nums">
            {formatCredits(member.availableQuarterCredits)}
          </CardContent>
        </Card>
        <Card role="group" aria-label={intl.formatMessage({ id: 'credits.escrowed' })}>
          <CardHeader>
            <CardTitle className="text-muted-foreground">
              <FormattedMessage id="credits.escrowed" />
            </CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold tabular-nums">
            {formatCredits(member.escrowedQuarterCredits)}
          </CardContent>
        </Card>
      </div>
      <Requests memberId={memberId} />
      <h3 className="pt-2 text-base font-semibold">
        <FormattedMessage id="credits.ledger.title" />
      </h3>
      <LedgerList entries={entries} />
      {detail.hasNextPage && (
        <Button
          variant="outline"
          className="h-11 w-full lg:h-8 lg:w-auto"
          onClick={() => void detail.fetchNextPage()}
          disabled={detail.isFetchingNextPage}
        >
          <FormattedMessage id="credits.loadMore" />
        </Button>
      )}
    </div>
  )
}
