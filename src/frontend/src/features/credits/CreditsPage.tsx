import { Lock, Wallet } from 'lucide-react'
import { FormattedMessage, useIntl } from 'react-intl'
import { Button } from '@/components/ui/button'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useMyAccount } from './api'
import { formatCredits } from './format'
import { LedgerList } from './LedgerList'
import { ErrorReference } from '@/components/ErrorReference'

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
          <ErrorReference error={account.error} />
        </AlertDescription>
      </Alert>
    )
  }
  if (!account.data) return null

  const [first] = account.data.pages
  if (!first) return null
  const entries = account.data.pages.flatMap((page) => page.ledger.items)

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2">
        <Card
          role="group"
          aria-label={intl.formatMessage({ id: 'credits.available' })}
          className="bg-primary text-primary-foreground"
        >
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-primary-foreground/80">
              <Wallet className="size-4 shrink-0" aria-hidden />
              <FormattedMessage id="credits.available" />
            </CardTitle>
          </CardHeader>
          <CardContent className="text-4xl font-semibold tabular-nums">
            {formatCredits(first.availableQuarterCredits)}
          </CardContent>
        </Card>
        <Card role="group" aria-label={intl.formatMessage({ id: 'credits.escrowed' })}>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-muted-foreground">
              <Lock className="size-4 shrink-0" aria-hidden />
              <FormattedMessage id="credits.escrowed" />
            </CardTitle>
          </CardHeader>
          <CardContent className="text-4xl font-semibold tabular-nums">
            {formatCredits(first.escrowedQuarterCredits)}
          </CardContent>
        </Card>
      </div>
      <h3 className="pt-2 text-base font-semibold">
        <FormattedMessage id="credits.ledger.title" />
      </h3>
      <LedgerList entries={entries} />
      {account.hasNextPage && (
        <Button
          variant="outline"
          className="h-11 w-full lg:h-8 lg:w-auto"
          onClick={() => void account.fetchNextPage()}
          disabled={account.isFetchingNextPage}
        >
          <FormattedMessage id="credits.loadMore" />
        </Button>
      )}
    </div>
  )
}
