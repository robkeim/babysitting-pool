import { ChevronDown } from 'lucide-react'
import { type ReactNode, useId, useState } from 'react'
import { FormattedMessage, useIntl } from 'react-intl'
import { formatCredits } from '@/features/credits'
import type { CostQuote, QuoteDetails } from './types'

interface CostReceiptProps {
  /** The backend's itemised quote; the receipt only formats it and never prices anything itself. */
  quote: CostQuote | undefined
  details: QuoteDetails
  available: number | undefined
  cannotAfford: boolean
  /** Adds a link to the pricing rules, opened in a new tab so a half-filled form is kept. */
  showRulesLink?: boolean
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
export function CostReceipt({
  quote,
  details,
  available,
  cannotAfford,
  showRulesLink = false,
}: CostReceiptProps) {
  const intl = useIntl()
  const titleId = useId()
  const detailsId = useId()
  const [expanded, setExpanded] = useState(false)
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
      aria-labelledby={titleId}
      className="rounded-lg bg-accent px-3 py-3 text-sm text-accent-foreground"
    >
      <h3 id={titleId} className="sr-only">
        <FormattedMessage id="requests.post.receipt.title" />
      </h3>
      <dl>
        <Row
          className="font-semibold"
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
        <p className="mt-1">
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
      <button
        type="button"
        className="mt-2 flex items-center gap-1 underline underline-offset-2"
        aria-expanded={expanded}
        aria-controls={detailsId}
        onClick={() => {
          setExpanded((open) => !open)
        }}
      >
        <FormattedMessage
          id={expanded ? 'requests.post.receipt.hide' : 'requests.post.receipt.show'}
        />
        <ChevronDown
          aria-hidden
          className={`size-4 transition-transform ${expanded ? 'rotate-180' : ''}`}
        />
      </button>
      <div id={detailsId} hidden={!expanded} className="mt-2">
        <dl className="space-y-1 border-t border-current/30 pt-2">
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
                  label={intl.formatMessage({
                    id: 'requests.post.location.RequestingFamilysHouse',
                  })}
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
        </dl>
        {showRulesLink && (
          <p className="mt-2">
            <a
              href="/how-it-works#pricing"
              target="_blank"
              rel="noopener"
              className="underline underline-offset-2"
            >
              <FormattedMessage id="requests.post.receipt.rules" />
            </a>
          </p>
        )}
      </div>
    </section>
  )
}
