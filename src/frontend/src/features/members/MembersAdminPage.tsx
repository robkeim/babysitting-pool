import { useState } from 'react'
import { Link } from '@tanstack/react-router'
import { Users } from 'lucide-react'
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
import { Card, CardContent } from '@/components/ui/card'
import { creditsValue, formatInstant } from '@/features/credits'
import { ApiError } from '@/lib/api/client'
import { type MessageId } from '@/lib/i18n'
import { useEraseMember, useMembers, useOffboardMember } from './api'
import type { AdminMemberView } from './types'

const KNOWN_FAILURES: Record<string, MessageId> = {
  member_not_found: 'error.member_not_found',
  cannot_offboard_admin: 'error.cannot_offboard_admin',
  member_not_closed: 'error.member_not_closed',
}

function failure(error: unknown): MessageId {
  return (
    (error instanceof ApiError ? KNOWN_FAILURES[error.code] : undefined) ??
    'common.somethingWentWrong'
  )
}

type Pending = 'offboard' | 'erase' | null

function MemberRow({ member }: { member: AdminMemberView }) {
  const intl = useIntl()
  const offboard = useOffboardMember()
  const erase = useEraseMember()
  const [pending, setPending] = useState<Pending>(null)
  const nameId = `member-${member.id}`
  const name = member.displayName
  const closed = member.closedAt !== null
  const busy = offboard.isPending || erase.isPending

  return (
    <Card role="group" aria-labelledby={nameId}>
      <CardContent className="space-y-3">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <div className="space-y-1">
            <p className="font-medium">
              <span id={nameId}>
                <Link
                  to="/admin/member/$memberId"
                  params={{ memberId: member.id }}
                  className="underline-offset-4 hover:underline"
                >
                  {name}
                </Link>
              </span>
              {member.isAdmin && (
                <span className="ml-2 text-sm text-muted-foreground">
                  <FormattedMessage id="members.admin.admin" />
                </span>
              )}
            </p>
            {!member.isErased && (
              <p className="text-sm [overflow-wrap:anywhere] text-muted-foreground">
                {member.email}
              </p>
            )}
            <p className="text-sm">
              {member.closedAt ? (
                <FormattedMessage
                  id={
                    member.isErased ? 'members.admin.status.erased' : 'members.admin.status.closed'
                  }
                  values={{ when: formatInstant(member.closedAt) }}
                />
              ) : (
                <FormattedMessage id="members.admin.status.open" />
              )}
            </p>
          </div>
          {pending === null && !member.isAdmin && !closed && (
            <Button
              variant="destructive"
              className="h-11 w-full lg:h-8 lg:w-auto"
              aria-label={intl.formatMessage({ id: 'members.admin.offboardAria' }, { name })}
              onClick={() => {
                offboard.reset()
                setPending('offboard')
              }}
            >
              <FormattedMessage id="members.admin.offboard" />
            </Button>
          )}
          {pending === null && closed && !member.isErased && (
            <Button
              variant="destructive"
              className="h-11 w-full lg:h-8 lg:w-auto"
              aria-label={intl.formatMessage({ id: 'members.admin.eraseAria' }, { name })}
              onClick={() => {
                erase.reset()
                setPending('erase')
              }}
            >
              <FormattedMessage id="members.admin.erase" />
            </Button>
          )}
        </div>

        <AlertDialog
          open={pending !== null}
          onOpenChange={(open) => {
            if (!open && !busy) setPending(null)
          }}
        >
          <AlertDialogContent>
            <AlertDialogTitle>
              <FormattedMessage
                id={
                  pending === 'erase'
                    ? 'members.admin.erase.confirmTitle'
                    : 'members.admin.offboard.confirmTitle'
                }
                values={{ name }}
              />
            </AlertDialogTitle>
            <AlertDialogDescription>
              <FormattedMessage
                id={
                  pending === 'erase'
                    ? 'members.admin.erase.confirmBody'
                    : 'members.admin.offboard.confirmBody'
                }
              />
            </AlertDialogDescription>
            <AlertDialogFooter>
              <AlertDialogClose
                render={<Button variant="outline" className="h-11 lg:h-8" disabled={busy} />}
              >
                <FormattedMessage id="members.admin.cancel" />
              </AlertDialogClose>
              <Button
                variant="destructive"
                className="h-11 lg:h-8"
                disabled={busy}
                onClick={() => {
                  const run = pending === 'erase' ? erase : offboard
                  run.mutate(member.id, {
                    onSettled: () => {
                      setPending(null)
                    },
                  })
                }}
              >
                <FormattedMessage
                  id={
                    pending === 'erase'
                      ? 'members.admin.erase.confirm'
                      : 'members.admin.offboard.confirm'
                  }
                />
              </Button>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        {offboard.isError && (
          <p role="alert" className="text-sm text-destructive">
            <FormattedMessage id={failure(offboard.error)} />
          </p>
        )}
        {erase.isError && (
          <p role="alert" className="text-sm text-destructive">
            <FormattedMessage id={failure(erase.error)} />
          </p>
        )}
        {offboard.isSuccess && (
          <p role="status" className="text-sm">
            <FormattedMessage
              id="members.admin.offboard.result"
              values={{
                canceled: offboard.data.canceled,
                withdrawn: offboard.data.withdrawn,
                settled: offboard.data.settled,
                burned: creditsValue(offboard.data.burnedQuarterCredits),
              }}
            />
          </p>
        )}
        {erase.isSuccess && (
          <p role="status" className="text-sm">
            <FormattedMessage id="members.admin.erase.result" />
          </p>
        )}
      </CardContent>
    </Card>
  )
}

/** The Admin's view of every Member, open and closed: offboard an open one, erase a closed one (spec §2A). */
export function MembersAdminPage() {
  const members = useMembers()

  return (
    <div className="space-y-4">
      {members.isError && (
        <Alert variant="destructive">
          <AlertTitle>
            <FormattedMessage id="members.admin.loadFailed.title" />
          </AlertTitle>
          <AlertDescription>
            <FormattedMessage id="members.admin.loadFailed.body" />
          </AlertDescription>
        </Alert>
      )}
      {members.data?.items.length === 0 && (
        <EmptyState icon={Users}>
          <FormattedMessage id="members.admin.empty" />
        </EmptyState>
      )}
      {members.data?.items.map((member) => (
        <MemberRow key={member.id} member={member} />
      ))}
    </div>
  )
}
