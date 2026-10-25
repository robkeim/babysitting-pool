import { FormattedMessage, useIntl } from 'react-intl'
import { Button } from '@/components/ui/button'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { type MessageId } from '@/lib/i18n'
import { useMyAccount } from './api'
import { formatCredits, formatInstant } from './format'

/** One message per Ledger entry kind; the backend enum is checked against it in CreditsPage.test.tsx. */
const KIND_MESSAGES: Partial<Record<string, MessageId>> = {
  Grant: 'credits.ledger.kind.Grant',
  Escrow: 'credits.ledger.kind.Escrow',
  Refund: 'credits.ledger.kind.Refund',
  Payout: 'credits.ledger.kind.Payout',
  Demurrage: 'credits.ledger.kind.Demurrage',
  Burn: 'credits.ledger.kind.Burn',
}

export function CreditsPage() {
  const intl = useIntl()
  const account = useMyAccount()

  if (account.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>
          <FormattedMessage id="credits.loadFailed.title" />
        </AlertTitle>
        <AlertDescription>
          <FormattedMessage id="credits.loadFailed.body" />
        </AlertDescription>
      </Alert>
    )
  }
  if (!account.data) return null

  const [first] = account.data.pages
  if (!first) return null
  const entries = account.data.pages.flatMap((page) => page.ledger.items)

  return (
    <div className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-2">
        <Card role="group" aria-label={intl.formatMessage({ id: 'credits.available' })}>
          <CardHeader>
            <CardTitle>
              <FormattedMessage id="credits.available" />
            </CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">
            {formatCredits(first.availableQuarterCredits)}
          </CardContent>
        </Card>
        <Card role="group" aria-label={intl.formatMessage({ id: 'credits.escrowed' })}>
          <CardHeader>
            <CardTitle>
              <FormattedMessage id="credits.escrowed" />
            </CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">
            {formatCredits(first.escrowedQuarterCredits)}
          </CardContent>
        </Card>
      </div>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>
              <FormattedMessage id="credits.ledger.when" />
            </TableHead>
            <TableHead>
              <FormattedMessage id="credits.ledger.kind" />
            </TableHead>
            <TableHead className="text-right">
              <FormattedMessage id="credits.ledger.credits" />
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {entries.map((entry) => (
            <TableRow key={entry.id}>
              <TableCell>{formatInstant(entry.occurredAt)}</TableCell>
              <TableCell>
                {(() => {
                  const id = KIND_MESSAGES[entry.kind]
                  return id ? <FormattedMessage id={id} /> : entry.kind
                })()}
              </TableCell>
              <TableCell className="text-right">
                {formatCredits(entry.amountQuarterCredits)}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      {account.hasNextPage && (
        <Button
          variant="outline"
          onClick={() => void account.fetchNextPage()}
          disabled={account.isFetchingNextPage}
        >
          <FormattedMessage id="credits.loadMore" />
        </Button>
      )}
    </div>
  )
}
