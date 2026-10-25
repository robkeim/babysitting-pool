import { useState, type SyntheticEvent } from 'react'
import { MailPlus } from 'lucide-react'
import { FormattedMessage, useIntl } from 'react-intl'
import { EmptyState } from '@/components/EmptyState'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { formatInstant } from '@/features/credits'
import { ApiError } from '@/lib/api/client'
import { type MessageId } from '@/lib/i18n'
import { useInvitations, useInviteMember, useResendInvitation } from './api'
import type { InvitationStatus, InvitationView } from './types'

const STATUS_MESSAGES: Record<InvitationStatus, MessageId> = {
  Pending: 'invitations.admin.status.Pending',
  Accepted: 'invitations.admin.status.Accepted',
  Expired: 'invitations.admin.status.Expired',
}

const KNOWN_FAILURES: Record<string, MessageId> = {
  invitation_not_found: 'error.invitation_not_found',
  invitation_already_accepted: 'error.invitation_already_accepted',
  invitation_already_sent: 'error.invitation_already_sent',
  already_a_member: 'error.already_a_member',
  invalid_request: 'invitations.admin.invite.invalid',
}

function failure(error: unknown): MessageId {
  return (
    (error instanceof ApiError ? KNOWN_FAILURES[error.code] : undefined) ??
    'common.somethingWentWrong'
  )
}

function InviteForm() {
  const invite = useInviteMember()
  const [email, setEmail] = useState('')

  function submit(event: SyntheticEvent) {
    event.preventDefault()
    const trimmed = email.trim()
    if (trimmed === '') return
    invite.mutate(trimmed, {
      onSuccess: () => {
        setEmail('')
      },
    })
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <h3>
            <FormattedMessage id="invitations.admin.invite.title" />
          </h3>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="space-y-4">
          <div className="space-y-2">
            <label htmlFor="invite-email" className="text-sm font-medium">
              <FormattedMessage id="invitations.admin.invite.email" />
            </label>
            <Input
              id="invite-email"
              type="email"
              autoComplete="off"
              value={email}
              onChange={(event) => {
                setEmail(event.target.value)
              }}
            />
          </div>
          {invite.isError && (
            <Alert variant="destructive">
              <AlertTitle>
                <FormattedMessage id="invitations.admin.invite.failed" />
              </AlertTitle>
              <AlertDescription>
                <FormattedMessage id={failure(invite.error)} />
              </AlertDescription>
            </Alert>
          )}
          <div className="flex flex-col gap-3 lg:flex-row lg:items-center">
            <Button
              type="submit"
              className="h-11 w-full lg:h-8 lg:w-auto"
              disabled={invite.isPending || email.trim() === ''}
            >
              <FormattedMessage id="invitations.admin.invite.send" />
            </Button>
            {invite.isSuccess && (
              <p role="status" className="text-sm">
                <FormattedMessage
                  id="invitations.admin.invite.sent"
                  values={{ email: invite.data.email }}
                />
              </p>
            )}
          </div>
        </form>
      </CardContent>
    </Card>
  )
}

function InvitationRow({ invitation }: { invitation: InvitationView }) {
  const intl = useIntl()
  const resend = useResendInvitation()
  const nameId = `invitation-${invitation.id}`

  return (
    <Card role="group" aria-labelledby={nameId}>
      <CardContent className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-1">
          <p className="font-medium">
            <span id={nameId}>{invitation.email}</span>
            {invitation.isAdmin && (
              <span className="ml-2 text-sm text-muted-foreground">
                <FormattedMessage id="invitations.admin.admin" />
              </span>
            )}
          </p>
          <p className="text-sm">
            <FormattedMessage id={STATUS_MESSAGES[invitation.status]} />
          </p>
          <p className="text-sm text-muted-foreground">
            {invitation.acceptedAt ? (
              <FormattedMessage
                id="invitations.admin.acceptedAt"
                values={{ when: formatInstant(invitation.acceptedAt) }}
              />
            ) : (
              <FormattedMessage
                id="invitations.admin.expiresAt"
                values={{ when: formatInstant(invitation.expiresAt) }}
              />
            )}
          </p>
          {resend.isError && (
            <p role="alert" className="text-sm text-destructive">
              <FormattedMessage id={failure(resend.error)} />
            </p>
          )}
          {resend.isSuccess && (
            <p role="status" className="text-sm">
              <FormattedMessage id="invitations.admin.resent" />
            </p>
          )}
        </div>
        {invitation.status !== 'Accepted' && (
          <Button
            variant="outline"
            className="h-11 w-full lg:h-8 lg:w-auto"
            disabled={resend.isPending}
            aria-label={intl.formatMessage(
              { id: 'invitations.admin.resendAria' },
              { email: invitation.email },
            )}
            onClick={() => {
              resend.mutate(invitation.id)
            }}
          >
            <FormattedMessage id="invitations.admin.resend" />
          </Button>
        )}
      </CardContent>
    </Card>
  )
}

/** The Admin's view of every Invitation: invite a person, see each one's state, re-send one (spec §2A). */
export function InvitationsAdminPage() {
  const invitations = useInvitations()

  return (
    <div className="space-y-4">
      <InviteForm />
      {invitations.isError && (
        <Alert variant="destructive">
          <AlertTitle>
            <FormattedMessage id="invitations.admin.loadFailed.title" />
          </AlertTitle>
          <AlertDescription>
            <FormattedMessage id="invitations.admin.loadFailed.body" />
          </AlertDescription>
        </Alert>
      )}
      {invitations.data?.items.length === 0 && (
        <EmptyState icon={MailPlus}>
          <FormattedMessage id="invitations.admin.empty" />
        </EmptyState>
      )}
      {invitations.data?.items.map((invitation) => (
        <InvitationRow key={invitation.id} invitation={invitation} />
      ))}
    </div>
  )
}
