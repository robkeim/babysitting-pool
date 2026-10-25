import { useEffect, type ReactNode } from 'react'
import { FormattedMessage, useIntl, type IntlShape } from 'react-intl'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { CostReceipt } from '@/features/requests'
import { formatCredits } from '@/features/credits'
import { useRules } from './api'
import type { PricingExample, RulesView } from './types'

/** Anchors a link can point at, e.g. `/how-it-works#pricing` from the post form. */
const SECTION_IDS = {
  credits: 'credits',
  pricing: 'pricing',
  requests: 'requests',
  demurrage: 'demurrage',
} as const

const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']

/** "6 hours" when the minutes are whole hours, else "90 minutes". */
function duration(intl: IntlShape, minutes: number) {
  return minutes % 60 === 0
    ? intl.formatMessage({ id: 'rules.hours' }, { count: minutes / 60 })
    : intl.formatMessage({ id: 'rules.minutes' }, { count: minutes })
}

/** The weekday in the Member's Language; 2024-01-07 was a Sunday. */
function weekday(intl: IntlShape, day: string) {
  return intl.formatDate(new Date(Date.UTC(2024, 0, 7 + DAYS.indexOf(day))), {
    weekday: 'long',
    timeZone: 'UTC',
  })
}

function Section({ id, title, children }: { id: string; title: ReactNode; children: ReactNode }) {
  return (
    <Card id={id} className="scroll-mt-20" role="region" aria-labelledby={`${id}-title`}>
      <CardHeader>
        <CardTitle>
          <h3 id={`${id}-title`} className="text-base font-semibold">
            {title}
          </h3>
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">{children}</CardContent>
    </Card>
  )
}

function Example({ example }: { example: PricingExample }) {
  const intl = useIntl()
  return (
    <div className="space-y-2">
      <p className="font-medium">
        <FormattedMessage
          id="rules.pricing.example"
          values={{
            children: example.childCount,
            minutes: example.durationMinutes,
            location: intl.formatMessage({ id: `requests.post.location.${example.location}` }),
          }}
        />
      </p>
      <CostReceipt
        quote={example.quote}
        details={{
          durationMinutes: example.durationMinutes,
          childCount: example.childCount,
          location: example.location,
          transport: example.transport,
          lunch: example.lunch,
          snack: example.snack,
          dinner: example.dinner,
        }}
        available={undefined}
        cannotAfford={false}
      />
    </div>
  )
}

function Rules({ rules }: { rules: RulesView }) {
  const intl = useIntl()
  const { prices } = rules

  return (
    <div className="space-y-4">
      <Section id={SECTION_IDS.credits} title={<FormattedMessage id="rules.credits.title" />}>
        <p>
          <FormattedMessage id="rules.credits.intro" />
        </p>
        <p>
          <FormattedMessage
            id="rules.credits.start"
            values={{ amount: formatCredits(rules.startingBalanceQuarterCredits) }}
          />
        </p>
        <p>
          <FormattedMessage id="rules.credits.floor" />
        </p>
      </Section>

      <Section id={SECTION_IDS.pricing} title={<FormattedMessage id="rules.pricing.title" />}>
        <p>
          <FormattedMessage
            id="rules.pricing.time"
            values={{ minutes: rules.durationIncrementMinutes }}
          />
        </p>
        <ul className="list-disc space-y-1 pl-5">
          <li>
            <FormattedMessage id="rules.pricing.firstChild" />
          </li>
          <li>
            <FormattedMessage
              id="rules.pricing.secondChild"
              values={{ rate: formatCredits(prices.secondChildHourlyQuarterCredits) }}
            />
          </li>
          <li>
            <FormattedMessage
              id="rules.pricing.otherChildren"
              values={{ rate: formatCredits(prices.additionalChildHourlyQuarterCredits) }}
            />
          </li>
          <li>
            <FormattedMessage
              id="rules.pricing.familyHouse"
              values={{ amount: formatCredits(prices.familyHouseQuarterCredits) }}
            />
          </li>
          <li>
            <FormattedMessage
              id="rules.pricing.transport"
              values={{
                oneWay: formatCredits(prices.oneWayTransportQuarterCredits),
                roundTrip: formatCredits(prices.roundTripTransportQuarterCredits),
              }}
            />
          </li>
          <li>
            <FormattedMessage
              id="rules.pricing.meals"
              values={{
                lunch: formatCredits(prices.lunchPerChildQuarterCredits),
                dinner: formatCredits(prices.dinnerPerChildQuarterCredits),
                snack: formatCredits(prices.snackPerChildQuarterCredits),
              }}
            />
          </li>
        </ul>
        <p>
          <FormattedMessage id="rules.pricing.exclusive" />
        </p>
        <h4 className="pt-1 font-semibold">
          <FormattedMessage id="rules.pricing.examples" />
        </h4>
        {rules.examples.map((example, index) => (
          <Example key={index} example={example} />
        ))}
      </Section>

      <Section id={SECTION_IDS.requests} title={<FormattedMessage id="rules.requests.title" />}>
        <ul className="list-disc space-y-1 pl-5">
          <li>
            <FormattedMessage id="rules.requests.post" />
          </li>
          <li>
            <FormattedMessage
              id="rules.requests.lead"
              values={{ lead: duration(intl, rules.minimumLeadTimeMinutes) }}
            />
          </li>
          <li>
            <FormattedMessage
              id="rules.requests.claim"
              values={{ cutoff: duration(intl, rules.claimCutoffMinutes) }}
            />
          </li>
          <li>
            <FormattedMessage id="rules.requests.payout" />
          </li>
          <li>
            <FormattedMessage id="rules.requests.cancel" />
          </li>
        </ul>
      </Section>

      <Section id={SECTION_IDS.demurrage} title={<FormattedMessage id="rules.demurrage.title" />}>
        <p>
          <FormattedMessage id="rules.demurrage.why" />
        </p>
        <p>
          <FormattedMessage
            id="rules.demurrage.charge"
            values={{
              days: rules.demurrageGraceDays,
              amount: formatCredits(rules.demurrageChargeQuarterCredits),
              day: weekday(intl, rules.demurrageDay),
              time: rules.demurrageTime,
              zone: rules.timeZone,
            }}
          />
        </p>
        <p>
          <FormattedMessage id="rules.demurrage.activity" />
        </p>
        <p>
          <FormattedMessage id="rules.demurrage.floor" />
        </p>
        <p>
          <FormattedMessage
            id="rules.demurrage.dormancy"
            values={{ weeks: rules.dormancyDays / 7 }}
          />
        </p>
      </Section>
    </div>
  )
}

export function HowItWorksPage() {
  const rules = useRules()

  // The sections only exist once the rules load, so a deep link scrolls to its anchor then.
  const loaded = rules.data !== undefined
  useEffect(() => {
    if (loaded) document.getElementById(window.location.hash.slice(1))?.scrollIntoView()
  }, [loaded])

  if (rules.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>
          <FormattedMessage id="rules.loadFailed.title" />
        </AlertTitle>
        <AlertDescription>
          <FormattedMessage id="rules.loadFailed.body" />
        </AlertDescription>
      </Alert>
    )
  }
  return rules.data ? <Rules rules={rules.data} /> : null
}
