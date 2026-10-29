import { useState, type SyntheticEvent } from 'react'
import { FormattedMessage } from 'react-intl'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Textarea } from '@/components/ui/textarea'
import { ApiError } from '@/lib/api/client'
import { type MessageId } from '@/lib/i18n'
import { AddressSearch } from './AddressSearch'
import { useMyProfile, useUpdateProfile } from './api'
import type { AddressSuggestion, ProfileView } from './types'

const INSTRUCTIONS_MAX_LENGTH = 300

const KNOWN_FAILURES: Record<string, MessageId> = {
  invalid_address: 'profile.failed.address',
  invalid_instructions: 'profile.failed.instructions',
  address_service_unavailable: 'profile.failed.unavailable',
}

function failure(error: unknown): MessageId {
  return (
    (error instanceof ApiError ? KNOWN_FAILURES[error.code] : undefined) ??
    'common.somethingWentWrong'
  )
}

function ProfileForm({ profile }: { profile: ProfileView }) {
  const update = useUpdateProfile()
  const [text, setText] = useState(profile.address ?? '')
  const [picked, setPicked] = useState<AddressSuggestion | null>(null)
  const [instructions, setInstructions] = useState(profile.instructions ?? '')

  // Typed text that is not a pick is not an address; the saved one stays valid while it is untouched.
  const editedWithoutPick = picked === null && text.trim() !== (profile.address ?? '')
  const hasAddress = picked !== null || profile.address !== null
  const area = picked ? `${picked.commune} (${picked.departmentNumber})` : profile.area

  function submit(event: SyntheticEvent) {
    event.preventDefault()
    update.mutate(
      picked ? { banId: picked.banId, label: picked.label, instructions } : { instructions },
    )
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
            <label htmlFor="address" className="text-sm font-medium">
              <FormattedMessage id="profile.address" />
            </label>
            <AddressSearch
              id="address"
              value={text}
              searching={editedWithoutPick}
              describedBy="address-hint"
              onChange={(value) => {
                setText(value)
                setPicked(null)
              }}
              onPick={(suggestion) => {
                setText(suggestion.label)
                setPicked(suggestion)
              }}
            />
            <p id="address-hint" className="text-sm text-muted-foreground">
              <FormattedMessage id="profile.address.hint" />
            </p>
            {editedWithoutPick && (
              <p className="text-sm text-destructive">
                <FormattedMessage id="profile.address.choose" />
              </p>
            )}
          </div>
          {area && (
            <p className="text-sm">
              <FormattedMessage id="profile.area.preview" values={{ area }} />
            </p>
          )}
          <div className="space-y-2">
            <label htmlFor="instructions" className="text-sm font-medium">
              <FormattedMessage id="profile.instructions" />
            </label>
            <Textarea
              id="instructions"
              maxLength={INSTRUCTIONS_MAX_LENGTH}
              value={instructions}
              onChange={(event) => {
                setInstructions(event.target.value)
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
            disabled={update.isPending || editedWithoutPick || !hasAddress}
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
