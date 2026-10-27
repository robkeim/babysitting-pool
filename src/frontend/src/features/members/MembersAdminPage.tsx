import { useState } from 'react'
import { Users } from 'lucide-react'
import { FormattedMessage, useIntl } from 'react-intl'
import { EmptyState } from '@/components/EmptyState'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { formatCredits, formatInstant } from '@/features/credits'
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
              <span id={nameId}>{name}</span>
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

        {pending !== null && (
          <Alert variant="destructive" role="alertdialog" aria-label={name}>
            <AlertTitle>
              <FormattedMessage
                id={
                  pending === 'offboard'
                    ? 'members.admin.offboard.confirmTitle'
                    : 'members.admin.erase.confirmTitle'
                }
                values={{ name }}
              />
            </AlertTitle>
            <AlertDescription className="space-y-3">
              <p>
                <FormattedMessage
                  id={
                    pending === 'offboard'
                      ? 'members.admin.offboard.confirmBody'
                      : 'members.admin.erase.confirmBody'
                  }
                />
              </p>
              <div className="flex flex-col gap-3 lg:flex-row">
                <Button
                  variant="destructive"
                  className="h-11 w-full lg:h-8 lg:w-auto"
                  disabled={busy}
                  onClick={() => {
                    const run = pending === 'offboard' ? offboard : erase
                    run.mutate(member.id, {
                      onSuccess: () => {
                        setPending(null)
                      },
                    })
                  }}
                >
                  <FormattedMessage
                    id={
                      pending === 'offboard'
                        ? 'members.admin.offboard.confirm'
                        : 'members.admin.erase.confirm'
                    }
                  />
                </Button>
                <Button
                  variant="outline"
                  className="h-11 w-full lg:h-8 lg:w-auto"
                  disabled={busy}
                  onClick={() => {
                    setPending(null)
                  }}
                >
                  <FormattedMessage id="members.admin.cancel" />
                </Button>
              </div>
            </AlertDescription>
          </Alert>
        )}

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
                cancelled: offboard.data.cancelled,
                withdrawn: offboard.data.withdrawn,
                settled: offboard.data.settled,
                burned: formatCredits(offboard.data.burnedQuarterCredits),
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
