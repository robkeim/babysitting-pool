import { useState, type SyntheticEvent } from 'react'
import { FormattedMessage } from 'react-intl'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { useAcceptInvitation, useInvitationPreview } from './api'

interface InvitationPageProps {
  token: string | undefined
  onJoined: () => void
}

/** Opened from an Invitation email: asks for a display name, then creates the account and signs in. */
export function InvitationPage({ token, onJoined }: InvitationPageProps) {
  const preview = useInvitationPreview(token ?? '')
  const accept = useAcceptInvitation(token ?? '')
  const [displayName, setDisplayName] = useState('')

  function submit(event: SyntheticEvent) {
    event.preventDefault()
    accept.mutate(displayName, { onSuccess: onJoined })
  }

  if (!token || preview.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>
          <FormattedMessage id="auth.invitation.unusable.title" />
        </AlertTitle>
        <AlertDescription>
          <FormattedMessage id="auth.invitation.unusable.body" />
        </AlertDescription>
      </Alert>
    )
  }
  if (!preview.data) return null

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <FormattedMessage id="auth.invitation.title" />
        </CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="space-y-4">
          <p>
            <FormattedMessage
              id="auth.invitation.joiningAs"
              values={{
                email: preview.data.email,
                strong: (chunks) => <strong>{chunks}</strong>,
              }}
            />
          </p>
          <div className="space-y-2">
            <label htmlFor="displayName" className="text-sm font-medium">
              <FormattedMessage id="auth.invitation.name" />
            </label>
            <Input
              id="displayName"
              autoComplete="name"
              required
              maxLength={60}
              value={displayName}
              onChange={(event) => {
                setDisplayName(event.target.value)
              }}
            />
          </div>
          {accept.isError && (
            <Alert variant="destructive">
              <AlertTitle>
                <FormattedMessage id="auth.invitation.failed.title" />
              </AlertTitle>
              <AlertDescription>
                <FormattedMessage id="auth.invitation.failed.body" />
              </AlertDescription>
            </Alert>
          )}
          <Button type="submit" disabled={accept.isPending}>
            <FormattedMessage id="auth.invitation.submit" />
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}
