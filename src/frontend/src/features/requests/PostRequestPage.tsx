import { useState, type SyntheticEvent } from 'react'
import { FormattedMessage, useIntl } from 'react-intl'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { formatCredits, useMyAccount } from '@/features/credits'
import { ApiError } from '@/lib/api/client'
import { type MessageId } from '@/lib/i18n'
import { useQuote, usePostRequest } from './api'
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
  'h-11 w-full rounded-lg border border-input bg-transparent px-2.5 text-base outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 lg:h-8 lg:text-sm dark:bg-input/30'

const POST_FAILURE_CODES: Partial<Record<string, MessageId>> = {
  below_minimum_lead_time: 'error.below_minimum_lead_time',
  no_available_credits: 'error.no_available_credits',
  insufficient_credits: 'error.insufficient_credits',
  concurrent_update: 'error.concurrent_update',
}

function postFailure(error: unknown): MessageId {
  const code = error instanceof ApiError ? error.code : ''
  return POST_FAILURE_CODES[code] ?? 'requests.post.failed.generic'
}

/** The post form: a live cost preview, then one submit that takes the cost into Escrow (spec §4.1). */
export function PostRequestPage({ onPosted }: PostRequestPageProps) {
  const intl = useIntl()
  const account = useMyAccount()
  const post = usePostRequest()
  const [startsAt, setStartsAt] = useState(defaultStartsAt)
  const [durationMinutes, setDurationMinutes] = useState(60)
  const [childCount, setChildCount] = useState(1)
  const [location, setLocation] = useState<BabysittingLocation>('SittersHouse')
  const [transport, setTransport] = useState<TransportOption>('None')
  const [lunch, setLunch] = useState(false)
  const [snack, setSnack] = useState(false)
  const [dinner, setDinner] = useState(false)
  const [area, setArea] = useState('')
  const [address, setAddress] = useState('')

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
  const cost = detailsValid ? quote.data?.costQuarterCredits : undefined
  const available = account.data?.pages[0]?.availableQuarterCredits
  const cannotAfford = cost !== undefined && available !== undefined && available < cost

  function changeLocation(next: BabysittingLocation) {
    setLocation(next)
    // Transport and the family-house premium are mutually exclusive (spec §3D).
    if (next === 'RequestingFamilysHouse') setTransport('None')
  }

  function submit(event: SyntheticEvent) {
    event.preventDefault()
    post.mutate(
      { ...details, startsAt: new Date(startsAt).toISOString(), area, address },
      { onSuccess: onPosted },
    )
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
                  {intl.formatMessage({ id: 'requests.post.location.RequestingFamilysHouse' })}
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
                disabled={location === 'RequestingFamilysHouse'}
                onChange={(event) => {
                  setTransport(event.target.value as TransportOption)
                }}
              >
                <option value="None">
                  {intl.formatMessage({ id: 'requests.post.transport.None' })}
                </option>
                <option value="OneWay">
                  {intl.formatMessage({ id: 'requests.post.transport.OneWay' })}
                </option>
                <option value="RoundTrip">
                  {intl.formatMessage({ id: 'requests.post.transport.RoundTrip' })}
                </option>
              </select>
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
              <FormattedMessage id="requests.post.meals.lunch" />
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
              <FormattedMessage id="requests.post.meals.snack" />
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
              <FormattedMessage id="requests.post.meals.dinner" />
            </label>
          </fieldset>
          <div className="space-y-2">
            <label htmlFor="area" className="text-sm font-medium">
              <FormattedMessage id="requests.post.area" />
            </label>
            <Input
              id="area"
              className="h-11 lg:h-8"
              required
              maxLength={100}
              value={area}
              onChange={(event) => {
                setArea(event.target.value)
              }}
            />
          </div>
          <div className="space-y-2">
            <label htmlFor="address" className="text-sm font-medium">
              <FormattedMessage id="requests.post.address" />
            </label>
            <Input
              id="address"
              className="h-11 lg:h-8"
              required
              maxLength={200}
              value={address}
              onChange={(event) => {
                setAddress(event.target.value)
              }}
            />
          </div>
          <p
            aria-live="polite"
            className="rounded-lg bg-accent px-3 py-2 text-sm text-accent-foreground"
          >
            {cost === undefined ? (
              <FormattedMessage
                id="requests.post.costUnknown"
                values={{ strong: (chunks) => <strong>{chunks}</strong> }}
              />
            ) : (
              <FormattedMessage
                id="requests.post.cost"
                values={{
                  cost: formatCredits(cost),
                  strong: (chunks) => <strong>{chunks}</strong>,
                }}
              />
            )}
            {available !== undefined && (
              <>
                {' · '}
                <FormattedMessage
                  id="requests.post.available"
                  values={{ available: formatCredits(available) }}
                />
              </>
            )}
          </p>
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
                <FormattedMessage id={postFailure(post.error)} />
              </AlertDescription>
            </Alert>
          )}
          <Button
            type="submit"
            className="h-11 w-full lg:h-8 lg:w-auto"
            disabled={post.isPending || !detailsValid || cannotAfford}
          >
            <FormattedMessage id="requests.post.submit" />
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}
