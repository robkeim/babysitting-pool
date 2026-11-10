import { useState } from 'react'
import { Link } from '@tanstack/react-router'
import { Moon } from 'lucide-react'
import { FormattedMessage, useIntl } from 'react-intl'
import { EmptyState } from '@/components/EmptyState'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import {
  AlertDialog,
  AlertDialogClose,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatCredits, formatInstant } from '@/features/credits'
import { type MessageId } from '@/lib/i18n'
import { useDormantMembers, usePoolOverview, useRunSettle } from './api'
import type { PoolOverview } from './types'
import { ErrorReference } from '@/components/ErrorReference'

const REQUEST_STATES: readonly [keyof PoolOverview['requests'], MessageId][] = [
  ['posted', 'requests.status.Posted'],
  ['claimed', 'requests.status.Claimed'],
  ['completed', 'requests.status.Completed'],
  ['canceled', 'requests.status.Canceled'],
  ['expired', 'requests.status.Expired'],
]

function Figure({ label, value }: { label: MessageId; value: string }) {
  const intl = useIntl()
  return (
    <Card role="group" aria-label={intl.formatMessage({ id: label })}>
      <CardHeader>
        <CardTitle className="text-muted-foreground">
          <FormattedMessage id={label} />
        </CardTitle>
      </CardHeader>
      <CardContent className="text-3xl font-semibold tabular-nums">{value}</CardContent>
    </Card>
  )
}

function Overview() {
  const overview = usePoolOverview()

  if (overview.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>
          <FormattedMessage id="members.pool.loadFailed.title" />
        </AlertTitle>
        <AlertDescription>
          <FormattedMessage id="members.admin.loadFailed.body" />
          <ErrorReference error={overview.error} />
        </AlertDescription>
      </Alert>
    )
  }
  if (!overview.data) return null

  const data = overview.data
  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <Figure
          label="members.pool.circulation"
          value={formatCredits(data.creditsInCirculationQuarterCredits)}
        />
        <Figure label="credits.escrowed" value={formatCredits(data.escrowedQuarterCredits)} />
        <Figure
          label="members.pool.burnedByDemurrage"
          value={formatCredits(data.burnedByDemurrageQuarterCredits)}
        />
        <Figure
          label="members.pool.burnedByOffboarding"
          value={formatCredits(data.burnedByOffboardingQuarterCredits)}
        />
        <Figure label="members.pool.openMembers" value={String(data.openMembers)} />
      </div>
      <h3 className="pt-2 text-base font-semibold">
        <FormattedMessage id="members.pool.requests" />
      </h3>
      <div className="grid gap-3 sm:grid-cols-3 lg:grid-cols-5">
        {REQUEST_STATES.map(([state, label]) => (
          <Figure key={state} label={label} value={String(data.requests[state])} />
        ))}
      </div>
    </div>
  )
}

function Dormant() {
  const dormant = useDormantMembers()

  if (dormant.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>
          <FormattedMessage id="members.dormant.loadFailed.title" />
        </AlertTitle>
        <AlertDescription>
          <FormattedMessage id="members.admin.loadFailed.body" />
          <ErrorReference error={dormant.error} />
        </AlertDescription>
      </Alert>
    )
  }
  if (!dormant.data) return null

  return (
    <div className="space-y-4">
      {dormant.data.items.length === 0 && (
        <EmptyState icon={Moon}>
          <FormattedMessage id="members.dormant.empty" />
        </EmptyState>
      )}
      {dormant.data.items.map((member) => (
        <Card key={member.id} role="group" aria-labelledby={`dormant-${member.id}`}>
          <CardContent className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <div className="min-w-0 space-y-1">
              <p className="font-medium [overflow-wrap:anywhere]">
                <Link
                  id={`dormant-${member.id}`}
                  to="/admin/member/$memberId"
                  params={{ memberId: member.id }}
                  className="underline-offset-4 hover:underline"
                >
                  {member.displayName}
                </Link>
              </p>
              <p className="text-sm [overflow-wrap:anywhere] text-muted-foreground">
                {member.email}
              </p>
              <p className="text-sm">
                <FormattedMessage
                  id="members.dormant.lastActivity"
                  values={{ when: formatInstant(member.lastActivityAt) }}
                />
              </p>
            </div>
            <p className="text-sm tabular-nums">
              <FormattedMessage
                id="members.dormant.available"
                values={{ available: formatCredits(member.availableQuarterCredits) }}
              />
            </p>
          </CardContent>
        </Card>
      ))}
    </div>
  )
}

function RunSettle() {
  const settle = useRunSettle()
  const [confirming, setConfirming] = useState(false)

  return (
    <div className="space-y-3">
      <h3 className="pt-2 text-base font-semibold">
        <FormattedMessage id="members.pool.settle.title" />
      </h3>
      <p className="text-sm text-muted-foreground">
        <FormattedMessage id="members.pool.settle.hint" />
      </p>
      <Button
        className="h-11 w-full lg:h-8 lg:w-auto"
        disabled={settle.isPending}
        onClick={() => {
          settle.reset()
          setConfirming(true)
        }}
      >
        <FormattedMessage id="members.pool.settle.run" />
      </Button>

      <AlertDialog
        open={confirming}
        onOpenChange={(open) => {
          if (!open && !settle.isPending) setConfirming(false)
        }}
      >
        <AlertDialogContent>
          <AlertDialogTitle>
            <FormattedMessage id="members.pool.settle.confirmTitle" />
          </AlertDialogTitle>
          <AlertDialogDescription>
            <FormattedMessage id="members.pool.settle.confirmBody" />
          </AlertDialogDescription>
          <AlertDialogFooter>
            <AlertDialogClose
              render={
                <Button variant="outline" className="h-11 lg:h-8" disabled={settle.isPending} />
              }
            >
              <FormattedMessage id="members.admin.cancel" />
            </AlertDialogClose>
            <Button
              className="h-11 lg:h-8"
              disabled={settle.isPending}
              onClick={() => {
                settle.mutate(undefined, {
                  onSettled: () => {
                    setConfirming(false)
                  },
                })
              }}
            >
              <FormattedMessage id="members.pool.settle.confirm" />
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {settle.isPending && (
        <p role="status" className="text-sm">
          <FormattedMessage id="members.pool.settle.running" />
        </p>
      )}
      {settle.isError && (
        <p role="alert" className="text-sm text-destructive">
          <FormattedMessage id="common.somethingWentWrong" />
          <ErrorReference error={settle.error} />
        </p>
      )}
      {settle.isSuccess && (
        <div role="status" className="space-y-1 text-sm">
          <p>
            <FormattedMessage id="members.pool.settle.result" values={settle.data} />
          </p>
          {settle.data.failed > 0 && (
            <p className="text-destructive">
              <FormattedMessage id="members.pool.settle.failed" values={settle.data} />
            </p>
          )}
        </div>
      )}
    </div>
  )
}

/** The Admin's totals for the whole pool, Settle on demand and the list of Dormant Members (spec §2A, §5, §6). */
export function PoolAdminPage() {
  return (
    <div className="space-y-4">
      <RunSettle />
      <Overview />
      <h3 className="pt-2 text-base font-semibold">
        <FormattedMessage id="members.dormant.title" />
      </h3>
      <p className="text-sm text-muted-foreground">
        <FormattedMessage id="members.dormant.hint" />
      </p>
      <Dormant />
    </div>
  )
}
