import { useState, type SyntheticEvent } from 'react'
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
        <AlertTitle>This invitation can&apos;t be used</AlertTitle>
        <AlertDescription>
          It may have been used already or have expired. Ask the co-op&apos;s Admin for a new one.
        </AlertDescription>
      </Alert>
    )
  }
  if (!preview.data) return null

  return (
    <Card>
      <CardHeader>
        <CardTitle>Join the co-op</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="space-y-4">
          <p>
            You&apos;re joining as <strong>{preview.data.email}</strong>.
          </p>
          <div className="space-y-2">
            <label htmlFor="displayName" className="text-sm font-medium">
              Your name
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
              <AlertTitle>We couldn&apos;t create your account</AlertTitle>
              <AlertDescription>
                Use 1 to 60 characters for your name. If it still fails, the invitation may have
                expired.
              </AlertDescription>
            </Alert>
          )}
          <Button type="submit" disabled={accept.isPending}>
            Create my account
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}
