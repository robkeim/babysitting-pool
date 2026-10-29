import { useState, type SyntheticEvent } from 'react'
import { FormattedMessage, useIntl } from 'react-intl'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { formatCredits, useMyAccount } from '@/features/credits'
import { useMyProfile } from '@/features/profile'
import { formatDuration, useRules } from '@/features/rules'
import { ApiError } from '@/lib/api/client'
import { type MessageId } from '@/lib/i18n'
import { useQuote, usePostRequest } from './api'
import { CostReceipt } from './CostReceipt'
import type { BabysittingLocation, QuoteDetails, TransportOption } from './types'

interface PostRequestPageProps {
  onPosted: () => void
}

const DURATION_STEP_MINUTES = 15
const START_STEP_SECONDS = 5 * 60

/** The current time rounded up to the next 5-minute mark, as a datetime-local value in local time. */
function defaultStartsAt(): string {
  const stepMs = START_STEP_SECONDS * 1000
  const rounded = new Date(Math.ceil(Date.now() / stepMs) * stepMs)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${String(rounded.getFullYear())}-${pad(rounded.getMonth() + 1)}-${pad(rounded.getDate())}T${pad(rounded.getHours())}:${pad(rounded.getMinutes())}`
}

const selectClass =
  'h-11 w-full rounded-lg border border-input bg-transparent px-2.5 text-base outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 lg:h-8 lg:text-sm disabled:cursor-not-allowed disabled:opacity-50 dark:bg-input/30'

const POST_FAILURE_CODES: Partial<Record<string, MessageId>> = {
  below_minimum_lead_time: 'error.below_minimum_lead_time',
  no_available_credits: 'error.no_available_credits',
  insufficient_credits: 'error.insufficient_credits',
  concurrent_update: 'error.concurrent_update',
  home_location_required: 'error.home_location_required',
}

function postFailure(error: unknown, leadKnown: boolean): MessageId {
  const code = error instanceof ApiError ? error.code : ''
  if (code === 'below_minimum_lead_time' && !leadKnown)
    return 'error.below_minimum_lead_time.unknown'
  return POST_FAILURE_CODES[code] ?? 'requests.post.failed.generic'
}

/** The post form: a live cost preview, then one submit that takes the cost into Escrow (spec §4.1). */
export function PostRequestPage({ onPosted }: PostRequestPageProps) {
  const intl = useIntl()
  const account = useMyAccount()
  const post = usePostRequest()
  const profile = useMyProfile()
  const rules = useRules()
  const [startsAt, setStartsAt] = useState(defaultStartsAt)
  const [durationMinutes, setDurationMinutes] = useState(60)
  const [childCount, setChildCount] = useState(1)
  const [location, setLocation] = useState<BabysittingLocation>('SittersHouse')
  const [transport, setTransport] = useState<TransportOption>('None')
  const [lunch, setLunch] = useState(false)
  const [snack, setSnack] = useState(false)
  const [dinner, setDinner] = useState(false)

  const details: QuoteDetails = {
    durationMinutes,
    childCount,
    location,
    transport,
    lunch,
    snack,
    dinner,
  }
  const durationValid =
    Number.isInteger(durationMinutes) &&
    durationMinutes > 0 &&
    durationMinutes % DURATION_STEP_MINUTES === 0
  const detailsValid = durationValid && Number.isInteger(childCount) && childCount >= 1
  const quote = useQuote(details, detailsValid)
  const currentQuote = detailsValid ? quote.data : undefined
  const cost = currentQuote?.costQuarterCredits
  const options = currentQuote?.options
  const lines = currentQuote?.lines
  const available = account.data?.pages[0]?.availableQuarterCredits
  const cannotAfford = cost !== undefined && available !== undefined && available < cost

  /** "Lunch (+1.50 credits)" once the backend has priced it, the bare label until then. */
  function priced(label: string, price: number | undefined) {
    return price === undefined
      ? label
      : intl.formatMessage({ id: 'requests.post.priced' }, { label, price: formatCredits(price) })
  }

  function perChild(rate: number) {
    return intl.formatMessage(
      { id: 'requests.post.perChild' },
      { rate: formatCredits(rate), count: childCount },
    )
  }

  const transportLocked = location === 'RequestingFamilysHouse'
  // Only a request at the family's house has a location; it comes from the profile, not from this form (spec §4.1).
  const home = profile.data?.area && profile.data.address ? profile.data : undefined
  const homeMissing = transportLocked && profile.isSuccess && !home

  function changeLocation(next: BabysittingLocation) {
    setLocation(next)
    // Transport and the family-house premium are mutually exclusive (spec §3D).
    if (next === 'RequestingFamilysHouse') setTransport('None')
  }

  function submit(event: SyntheticEvent) {
    event.preventDefault()
    post.mutate({ ...details, startsAt: new Date(startsAt).toISOString() }, { onSuccess: onPosted })
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <FormattedMessage id="requests.post.title" />
        </CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="space-y-4">
          <div className="space-y-2">
            <label htmlFor="startsAt" className="text-sm font-medium">
              <FormattedMessage id="requests.post.startsAt" />
            </label>
            <Input
              id="startsAt"
              className="h-11 lg:h-8"
              type="datetime-local"
              step={START_STEP_SECONDS}
              required
              value={startsAt}
              onChange={(event) => {
                setStartsAt(event.target.value)
              }}
            />
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <label htmlFor="duration" className="text-sm font-medium">
                <FormattedMessage id="requests.post.duration" />
              </label>
              <Input
                id="duration"
                className="h-11 lg:h-8"
                type="number"
                required
                min={DURATION_STEP_MINUTES}
                step={DURATION_STEP_MINUTES}
                aria-invalid={!durationValid}
                aria-describedby={durationValid ? undefined : 'duration-error'}
                value={Number.isNaN(durationMinutes) ? '' : durationMinutes}
                onChange={(event) => {
                  setDurationMinutes(event.target.valueAsNumber)
                }}
              />
              {!durationValid && (
                <p id="duration-error" role="alert" className="text-sm text-destructive">
                  <FormattedMessage id="requests.post.duration.invalid" />
                </p>
              )}
              {durationValid && lines && (
                <p className="text-sm text-muted-foreground">
                  <FormattedMessage
                    id="requests.post.duration.hint"
                    values={{
                      rate: formatCredits(lines.hourlyRateQuarterCredits),
                      count: childCount,
                    }}
                  />
                </p>
              )}
            </div>
            <div className="space-y-2">
              <label htmlFor="children" className="text-sm font-medium">
                <FormattedMessage id="requests.post.children" />
              </label>
              <Input
                id="children"
                className="h-11 lg:h-8"
                type="number"
                required
                min={1}
                step={1}
                value={Number.isNaN(childCount) ? '' : childCount}
                onChange={(event) => {
                  setChildCount(event.target.valueAsNumber)
                }}
              />
              {options && (
                <p className="text-sm text-muted-foreground">
                  <FormattedMessage
                    id="requests.post.children.hint"
                    values={{
                      second: formatCredits(options.secondChildHourlyQuarterCredits),
                      additional: formatCredits(options.additionalChildHourlyQuarterCredits),
                    }}
                  />
                </p>
              )}
            </div>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <label htmlFor="location" className="text-sm font-medium">
                <FormattedMessage id="requests.post.location" />
              </label>
              <select
                id="location"
                className={selectClass}
                value={location}
                onChange={(event) => {
                  changeLocation(event.target.value as BabysittingLocation)
                }}
              >
                <option value="SittersHouse">
                  {intl.formatMessage({ id: 'requests.post.location.SittersHouse' })}
                </option>
                <option value="RequestingFamilysHouse">
                  {priced(
                    intl.formatMessage({ id: 'requests.post.location.RequestingFamilysHouse' }),
                    options?.familyHouseQuarterCredits,
                  )}
                </option>
              </select>
            </div>
            <div className="space-y-2">
              <label htmlFor="transport" className="text-sm font-medium">
                <FormattedMessage id="requests.post.transport" />
              </label>
              <select
                id="transport"
                className={selectClass}
                value={transport}
                disabled={transportLocked}
                aria-describedby={transportLocked ? 'transport-hint' : undefined}
                onChange={(event) => {
                  setTransport(event.target.value as TransportOption)
                }}
              >
                <option value="None">
                  {intl.formatMessage({ id: 'requests.post.transport.None' })}
                </option>
                <option value="OneWay">
                  {priced(
                    intl.formatMessage({ id: 'requests.post.transport.OneWay' }),
                    options?.oneWayTransportQuarterCredits,
                  )}
                </option>
                <option value="RoundTrip">
                  {priced(
                    intl.formatMessage({ id: 'requests.post.transport.RoundTrip' }),
                    options?.roundTripTransportQuarterCredits,
                  )}
                </option>
              </select>
              {transportLocked && (
                <p id="transport-hint" className="text-sm text-muted-foreground">
                  <FormattedMessage id="requests.post.transport.unavailable" />
                </p>
              )}
            </div>
          </div>
          <fieldset className="space-y-2">
            <legend className="text-sm font-medium">
              <FormattedMessage id="requests.post.meals" />
            </legend>
            <label className="flex min-h-11 items-center gap-3 text-sm lg:min-h-0 lg:gap-2">
              <input
                type="checkbox"
                className="size-5 accent-primary lg:size-4"
                checked={lunch}
                onChange={(event) => {
                  setLunch(event.target.checked)
                }}
              />
              <span>
                {priced(
                  intl.formatMessage({ id: 'requests.post.meals.lunch' }),
                  options?.lunchQuarterCredits,
                )}
                {options && (
                  <span className="block text-xs text-muted-foreground">
                    {perChild(options.lunchPerChildQuarterCredits)}
                  </span>
                )}
              </span>
            </label>
            <label className="flex min-h-11 items-center gap-3 text-sm lg:min-h-0 lg:gap-2">
              <input
                type="checkbox"
                className="size-5 accent-primary lg:size-4"
                checked={snack}
                onChange={(event) => {
                  setSnack(event.target.checked)
                }}
              />
              <span>
                {priced(
                  intl.formatMessage({ id: 'requests.post.meals.snack' }),
                  options?.snackQuarterCredits,
                )}
                {options && (
                  <span className="block text-xs text-muted-foreground">
                    {perChild(options.snackPerChildQuarterCredits)}
                  </span>
                )}
              </span>
            </label>
            <label className="flex min-h-11 items-center gap-3 text-sm lg:min-h-0 lg:gap-2">
              <input
                type="checkbox"
                className="size-5 accent-primary lg:size-4"
                checked={dinner}
                onChange={(event) => {
                  setDinner(event.target.checked)
                }}
              />
              <span>
                {priced(
                  intl.formatMessage({ id: 'requests.post.meals.dinner' }),
                  options?.dinnerQuarterCredits,
                )}
                {options && (
                  <span className="block text-xs text-muted-foreground">
                    {perChild(options.dinnerPerChildQuarterCredits)}
                  </span>
                )}
              </span>
            </label>
          </fieldset>
          {transportLocked && home && (
            <p className="text-sm text-muted-foreground">
              <FormattedMessage
                id="requests.post.home.using"
                values={{ area: home.area, address: home.address }}
              />
              <a
                href="/profile"
                target="_blank"
                rel="noreferrer"
                className="block text-primary underline underline-offset-4"
              >
                <FormattedMessage id="requests.post.home.change" />
              </a>
            </p>
          )}
          {homeMissing && (
            <Alert variant="destructive">
              <AlertTitle>
                <FormattedMessage id="requests.post.home.missing.title" />
              </AlertTitle>
              <AlertDescription>
                <FormattedMessage id="requests.post.home.missing.body" />
                <a
                  href="/profile"
                  target="_blank"
                  rel="noreferrer"
                  className="block underline underline-offset-4"
                >
                  <FormattedMessage id="requests.post.home.missing.link" />
                </a>
              </AlertDescription>
            </Alert>
          )}
          <CostReceipt
            quote={currentQuote}
            details={details}
            available={available}
            cannotAfford={cannotAfford}
            showRulesLink
          />
          {cannotAfford && (
            <Alert variant="destructive">
              <AlertTitle>
                <FormattedMessage id="requests.post.cannotAfford.title" />
              </AlertTitle>
              <AlertDescription>
                <FormattedMessage id="requests.post.cannotAfford.body" />
              </AlertDescription>
            </Alert>
          )}
          {post.isError && (
            <Alert variant="destructive">
              <AlertTitle>
                <FormattedMessage id="requests.post.failed.title" />
              </AlertTitle>
              <AlertDescription>
                <FormattedMessage
                  id={postFailure(post.error, rules.data !== undefined)}
                  values={{
                    lead: rules.data ? formatDuration(intl, rules.data.minimumLeadTimeMinutes) : '',
                  }}
                />
              </AlertDescription>
            </Alert>
          )}
          <Button
            type="submit"
            className="h-11 w-full lg:h-8 lg:w-auto"
            disabled={post.isPending || !detailsValid || cannotAfford || homeMissing}
          >
            <FormattedMessage id="requests.post.submit" />
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}
