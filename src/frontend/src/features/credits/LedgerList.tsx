import { Banknote, Flame, Gift, Hourglass, Lock, Undo2, type LucideIcon } from 'lucide-react'
import { FormattedMessage } from 'react-intl'
import { Card, CardContent } from '@/components/ui/card'
import { type MessageId } from '@/lib/i18n'
import { formatCredits, formatInstant } from './format'
import { type LedgerEntryKind, type LedgerEntryView } from './types'

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

/** Ledger entries as a card of rows, in the order given. Used for a Member's own Ledger and the Admin's read-only view. */
export function LedgerList({ entries }: { entries: readonly LedgerEntryView[] }) {
  return (
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
  )
}
