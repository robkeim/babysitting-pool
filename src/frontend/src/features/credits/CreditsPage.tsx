import {
  Banknote,
  Flame,
  Gift,
  Hourglass,
  Lock,
  Undo2,
  Wallet,
  type LucideIcon,
} from 'lucide-react'
import { FormattedMessage, useIntl } from 'react-intl'
import { Button } from '@/components/ui/button'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { type MessageId } from '@/lib/i18n'
import { useMyAccount } from './api'
import { formatCredits, formatInstant } from './format'
import { type LedgerEntryKind } from './types'

/** One message per Ledger entry kind; a kind with no message fails the typecheck. */
const KIND_MESSAGES: Record<LedgerEntryKind, MessageId> = {
  Grant: 'credits.ledger.kind.Grant',
  Escrow: 'credits.ledger.kind.Escrow',
  Refund: 'credits.ledger.kind.Refund',
  Payout: 'credits.ledger.kind.Payout',
  Demurrage: 'credits.ledger.kind.Demurrage',
  Burn: 'credits.ledger.kind.Burn',
}

const KIND_ICONS: Record<LedgerEntryKind, LucideIcon> = {
  Grant: Gift,
  Escrow: Lock,
  Refund: Undo2,
  Payout: Banknote,
  Demurrage: Hourglass,
  Burn: Flame,
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
      <Card>
        <CardContent>
          <ul className="divide-y">
            {entries.map((entry) => {
              const Icon = KIND_ICONS[entry.kind]
              return (
                <li key={entry.id} className="flex items-center gap-3 py-3 first:pt-0 last:pb-0">
                  <span
                    aria-hidden
                    className="grid size-9 shrink-0 place-items-center rounded-full bg-muted text-muted-foreground"
                  >
                    <Icon className="size-4" />
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="font-medium [overflow-wrap:anywhere]">
                      <FormattedMessage id={KIND_MESSAGES[entry.kind]} />
                    </p>
                    <p className="text-xs [overflow-wrap:anywhere] text-muted-foreground">
                      {formatInstant(entry.occurredAt)}
                    </p>
                  </div>
                  <p className="shrink-0 font-semibold tabular-nums">
                    {formatCredits(entry.amountQuarterCredits)}
                  </p>
                </li>
              )
            })}
          </ul>
        </CardContent>
      </Card>
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
