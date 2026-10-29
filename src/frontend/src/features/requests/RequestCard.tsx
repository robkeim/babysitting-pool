import { CalendarClock, Clock, Coins, House, MapPin, NotebookText } from 'lucide-react'
import { type ReactNode } from 'react'
import { FormattedMessage } from 'react-intl'
import { Card, CardContent } from '@/components/ui/card'
import { formatCredits, formatInstant } from '@/features/credits'
import type { RequestStatus } from './types'

interface RequestCardProps {
  startsAt: string
  /** The rough area; null when the babysitting is at the Sitter's house, where it does not apply. */
  area?: string | null
  /** The exact address, only for the Requester and the Sitter who claimed it (spec §4.6). */
  address?: string | null
  /** The address instructions, shown on the same terms as the address (ADR 0020). */
  instructions?: string | null
  durationMinutes: number
  childCount: number
  costQuarterCredits: number
  status?: RequestStatus
  /** The one thing the viewer can do with it, if anything. */
  action?: ReactNode
}

const STATUS_CLASSES: Record<RequestStatus, string> = {
  Posted: 'bg-accent text-accent-foreground',
  Claimed: 'bg-primary text-primary-foreground',
  Completed: 'bg-success/15 text-success',
  Cancelled: 'bg-muted text-muted-foreground',
  Expired: 'bg-muted text-muted-foreground',
}

function Line({ icon: Icon, className, children }: LineProps) {
  return (
    <p className={`flex items-start gap-2 ${className ?? ''}`}>
      <Icon className="mt-0.5 size-4 shrink-0" aria-hidden />
      <span className="min-w-0 [overflow-wrap:anywhere]">{children}</span>
    </p>
  )
}

interface LineProps {
  icon: typeof MapPin
  className?: string
  children: ReactNode
}

/** One Request as a card: when, where, how long and what it costs, with a state and an action when given. */
export function RequestCard({
  startsAt,
  area,
  address,
  instructions,
  durationMinutes,
  childCount,
  costQuarterCredits,
  status,
  action,
}: RequestCardProps) {
  return (
    <Card>
      <CardContent className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="min-w-0 space-y-2">
          <Line icon={CalendarClock} className="font-semibold">
            {formatInstant(startsAt)}
          </Line>
          <Line icon={MapPin} className="text-muted-foreground">
            {area ?? <FormattedMessage id="requests.post.location.SittersHouse" />}
          </Line>
          {address && (
            <Line icon={House} className="text-muted-foreground">
              {address}
            </Line>
          )}
          {instructions && (
            <Line icon={NotebookText} className="text-muted-foreground">
              <span className="sr-only">
                <FormattedMessage id="requests.instructions" />
              </span>
              {instructions}
            </Line>
          )}
          <Line icon={Clock} className="text-muted-foreground">
            <FormattedMessage
              id="requests.details"
              values={{ minutes: durationMinutes, children: childCount }}
            />
          </Line>
        </div>
        <div className="flex flex-col gap-3 lg:items-end">
          <div className="flex flex-wrap items-center gap-2">
            {status && (
              <span
                className={`rounded-full px-3 py-1 text-sm font-medium ${STATUS_CLASSES[status]}`}
              >
                <FormattedMessage id={`requests.status.${status}`} />
              </span>
            )}
            <p className="flex items-center gap-1.5 rounded-full bg-accent px-3 py-1 font-semibold text-accent-foreground tabular-nums">
              <Coins className="size-4" aria-hidden />
              <span className="sr-only">
                <FormattedMessage id="requests.open.credits" />
              </span>
              {formatCredits(costQuarterCredits)}
            </p>
          </div>
          {action}
        </div>
      </CardContent>
    </Card>
  )
}
