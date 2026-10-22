import { useState, type SyntheticEvent } from 'react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { formatCredits, useMyAccount } from '@/features/credits'
import { ApiError } from '@/lib/api/client'
import { useQuote, usePostRequest } from './api'
import type { BabysittingLocation, QuoteDetails, TransportOption } from './types'

interface PostRequestPageProps {
  onPosted: () => void
}

const DURATION_STEP_MINUTES = 15

const selectClass =
  'h-8 w-full rounded-lg border border-input bg-transparent px-2.5 text-base outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 md:text-sm dark:bg-input/30'

function postFailure(error: unknown): string {
  const code = error instanceof ApiError ? error.code : ''
  switch (code) {
    case 'below_minimum_lead_time':
      return 'That start time is too soon. Pick a later start.'
    case 'no_available_credits':
      return 'You have no available credits, so you can’t post a request until you earn some.'
    case 'insufficient_credits':
      return 'You don’t have enough available credits for this request.'
    case 'concurrent_update':
      return 'Your balance changed while you were posting. Please try again.'
    default:
      return 'Please check the details and try again.'
  }
}

/** The post form: a live cost preview, then one submit that takes the cost into Escrow (spec §4.1). */
export function PostRequestPage({ onPosted }: PostRequestPageProps) {
  const account = useMyAccount()
  const post = usePostRequest()
  const [startsAt, setStartsAt] = useState('')
  const [durationMinutes, setDurationMinutes] = useState(60)
  const [childCount, setChildCount] = useState(1)
  const [location, setLocation] = useState<BabysittingLocation>('SittersHouse')
  const [transport, setTransport] = useState<TransportOption>('None')
  const [lunchOrDinner, setLunchOrDinner] = useState(false)
  const [snack, setSnack] = useState(false)
  const [area, setArea] = useState('')
  const [address, setAddress] = useState('')

  const details: QuoteDetails = {
    durationMinutes,
    childCount,
    location,
    transport,
    lunchOrDinner,
    snack,
  }
  const detailsValid =
    Number.isInteger(durationMinutes) &&
    durationMinutes > 0 &&
    durationMinutes % DURATION_STEP_MINUTES === 0 &&
    Number.isInteger(childCount) &&
    childCount >= 1
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
        <CardTitle>Post a request</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="space-y-4">
          <div className="space-y-2">
            <label htmlFor="startsAt" className="text-sm font-medium">
              Start time (your local time)
            </label>
            <Input
              id="startsAt"
              type="datetime-local"
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
                Duration (minutes, in steps of 15)
              </label>
              <Input
                id="duration"
                type="number"
                required
                min={DURATION_STEP_MINUTES}
                step={DURATION_STEP_MINUTES}
                value={Number.isNaN(durationMinutes) ? '' : durationMinutes}
                onChange={(event) => {
                  setDurationMinutes(event.target.valueAsNumber)
                }}
              />
            </div>
            <div className="space-y-2">
              <label htmlFor="children" className="text-sm font-medium">
                Number of children
              </label>
              <Input
                id="children"
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
                Location
              </label>
              <select
                id="location"
                className={selectClass}
                value={location}
                onChange={(event) => {
                  changeLocation(event.target.value as BabysittingLocation)
                }}
              >
                <option value="SittersHouse">Sitter&apos;s house</option>
                <option value="RequestingFamilysHouse">Requesting family&apos;s house</option>
              </select>
            </div>
            <div className="space-y-2">
              <label htmlFor="transport" className="text-sm font-medium">
                Transport
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
                <option value="None">No transport</option>
                <option value="OneWay">One way</option>
                <option value="RoundTrip">Round trip</option>
              </select>
            </div>
          </div>
          <fieldset className="space-y-2">
            <legend className="text-sm font-medium">Meals</legend>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={lunchOrDinner}
                onChange={(event) => {
                  setLunchOrDinner(event.target.checked)
                }}
              />
              Lunch or dinner
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={snack}
                onChange={(event) => {
                  setSnack(event.target.checked)
                }}
              />
              Snack
            </label>
          </fieldset>
          <div className="space-y-2">
            <label htmlFor="area" className="text-sm font-medium">
              Area (shown to other members)
            </label>
            <Input
              id="area"
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
              Exact address (shown only to the sitter once they claim)
            </label>
            <Input
              id="address"
              required
              maxLength={200}
              value={address}
              onChange={(event) => {
                setAddress(event.target.value)
              }}
            />
          </div>
          <p aria-live="polite" className="text-sm">
            Cost: <strong>{cost === undefined ? '—' : `${formatCredits(cost)} credits`}</strong>
            {available !== undefined && <> · You have {formatCredits(available)} available</>}
          </p>
          {cannotAfford && (
            <Alert variant="destructive">
              <AlertTitle>Not enough credits</AlertTitle>
              <AlertDescription>
                This request costs more than your available balance.
              </AlertDescription>
            </Alert>
          )}
          {post.isError && (
            <Alert variant="destructive">
              <AlertTitle>We couldn&apos;t post your request</AlertTitle>
              <AlertDescription>{postFailure(post.error)}</AlertDescription>
            </Alert>
          )}
          <Button type="submit" disabled={post.isPending || !detailsValid || cannotAfford}>
            Post request
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}
