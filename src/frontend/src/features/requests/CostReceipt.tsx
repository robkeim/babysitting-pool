import { type ReactNode } from 'react'
import { FormattedMessage, useIntl } from 'react-intl'
import { formatCredits } from '@/features/credits'
import type { CostQuote, QuoteDetails } from './types'

interface CostReceiptProps {
  /** The backend's itemised quote; the receipt only formats it and never prices anything itself. */
  quote: CostQuote | undefined
  details: QuoteDetails
  available: number | undefined
  cannotAfford: boolean
}

function Row({
  label,
  amount,
  className,
}: {
  label: ReactNode
  amount: ReactNode
  className?: string
}) {
  return (
    <div className={`flex justify-between gap-4 ${className ?? ''}`}>
      <dt>{label}</dt>
      <dd className="tabular-nums">{amount}</dd>
    </div>
  )
}

/** The post form's cost breakdown, like a receipt: one row per cost driver, then the total (spec §3). */
export function CostReceipt({ quote, details, available, cannotAfford }: CostReceiptProps) {
  const intl = useIntl()
  const lines = quote?.lines
  const options = quote?.options
  const meal = (id: 'lunch' | 'snack' | 'dinner', amount: number, perChild: number) => (
    <Row
      key={id}
      label={intl.formatMessage(
        { id: 'requests.post.receipt.meal' },
        {
          meal: intl.formatMessage({ id: `requests.post.meals.${id}` }),
          perChild: intl.formatMessage(
            { id: 'requests.post.perChild' },
            { rate: formatCredits(perChild), count: details.childCount },
          ),
        },
      )}
      amount={formatCredits(amount)}
    />
  )

  return (
    <section
      aria-live="polite"
      aria-labelledby="receipt-title"
      className="rounded-lg bg-accent px-3 py-3 text-sm text-accent-foreground"
    >
      <h3 id="receipt-title" className="mb-2 font-medium">
        <FormattedMessage id="requests.post.receipt.title" />
      </h3>
      <dl className="space-y-1">
        {lines && options && (
          <>
            <Row
              label={intl.formatMessage(
                { id: 'requests.post.receipt.time' },
                {
                  minutes: details.durationMinutes,
                  rate: formatCredits(lines.hourlyRateQuarterCredits),
                },
              )}
              amount={formatCredits(lines.timeQuarterCredits)}
            />
            {lines.locationQuarterCredits > 0 && (
              <Row
                label={intl.formatMessage({ id: 'requests.post.location.RequestingFamilysHouse' })}
                amount={formatCredits(lines.locationQuarterCredits)}
              />
            )}
            {lines.transportQuarterCredits > 0 && (
              <Row
                label={intl.formatMessage(
                  { id: 'requests.post.receipt.transport' },
                  {
                    option: intl.formatMessage({
                      id: `requests.post.transport.${details.transport}`,
                    }),
                  },
                )}
                amount={formatCredits(lines.transportQuarterCredits)}
              />
            )}
            {lines.lunchQuarterCredits > 0 &&
              meal('lunch', lines.lunchQuarterCredits, options.lunchPerChildQuarterCredits)}
            {lines.snackQuarterCredits > 0 &&
              meal('snack', lines.snackQuarterCredits, options.snackPerChildQuarterCredits)}
            {lines.dinnerQuarterCredits > 0 &&
              meal('dinner', lines.dinnerQuarterCredits, options.dinnerPerChildQuarterCredits)}
          </>
        )}
        <Row
          className="mt-2 border-t border-current/30 pt-2 font-semibold"
          label={<FormattedMessage id="requests.post.receipt.total" />}
          amount={
            quote ? (
              <FormattedMessage
                id="requests.post.receipt.amount"
                values={{ amount: formatCredits(quote.costQuarterCredits) }}
              />
            ) : (
              <FormattedMessage id="requests.post.receipt.unknown" />
            )
          }
        />
      </dl>
      {available !== undefined && (
        <p className="mt-2">
          <FormattedMessage
            id="requests.post.available"
            values={{ available: formatCredits(available) }}
          />
          {quote && !cannotAfford && (
            <>
              {' · '}
              <FormattedMessage
                id="requests.post.receipt.after"
                values={{ after: formatCredits(available - quote.costQuarterCredits) }}
              />
            </>
          )}
        </p>
      )}
    </section>
  )
}
