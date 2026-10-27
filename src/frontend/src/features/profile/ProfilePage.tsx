import { useState, type SyntheticEvent } from 'react'
import { FormattedMessage } from 'react-intl'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { ApiError } from '@/lib/api/client'
import { type MessageId } from '@/lib/i18n'
import { useMyProfile, useUpdateProfile } from './api'
import type { ProfileView } from './types'

const KNOWN_FAILURES: Record<string, MessageId> = {
  invalid_area: 'profile.failed.area',
  invalid_address: 'profile.failed.address',
}

function failure(error: unknown): MessageId {
  return (
    (error instanceof ApiError ? KNOWN_FAILURES[error.code] : undefined) ??
    'common.somethingWentWrong'
  )
}

function ProfileForm({ profile }: { profile: ProfileView }) {
  const update = useUpdateProfile()
  const [area, setArea] = useState(profile.area ?? '')
  const [address, setAddress] = useState(profile.address ?? '')

  function submit(event: SyntheticEvent) {
    event.preventDefault()
    update.mutate({ area, address })
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <FormattedMessage id="profile.home.title" />
        </CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="space-y-4">
          <p className="text-sm text-muted-foreground">
            <FormattedMessage id="profile.home.intro" />
          </p>
          <div className="space-y-2">
            <label htmlFor="area" className="text-sm font-medium">
              <FormattedMessage id="profile.area" />
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
              <FormattedMessage id="profile.address" />
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
          {update.isError && (
            <Alert variant="destructive">
              <AlertTitle>
                <FormattedMessage id="profile.failed.title" />
              </AlertTitle>
              <AlertDescription>
                <FormattedMessage id={failure(update.error)} />
              </AlertDescription>
            </Alert>
          )}
          {update.isSuccess && (
            <p role="status" className="text-sm text-success">
              <FormattedMessage id="profile.saved" />
            </p>
          )}
          <Button
            type="submit"
            className="h-11 w-full lg:h-8 lg:w-auto"
            disabled={update.isPending}
          >
            <FormattedMessage id="profile.save" />
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}

/** "Profile": the Member's area and exact address, set once instead of on every Request (spec §2A). */
export function ProfilePage() {
  const profile = useMyProfile()

  if (profile.isError) {
    return (
      <Alert variant="destructive">
        <AlertDescription>
          <FormattedMessage id="common.somethingWentWrong" />
        </AlertDescription>
      </Alert>
    )
  }
  if (!profile.data) return null
  return <ProfileForm profile={profile.data} />
}
