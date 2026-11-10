import { useState, type SyntheticEvent } from 'react'
import { FormattedMessage } from 'react-intl'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { ApiError } from '@/lib/api/client'
import { type MessageId } from '@/lib/i18n'
import { useEditNotes } from './api'
import { NotesField } from './NotesField'
import type { RequestStatus } from './types'
import { ErrorReference } from '@/components/ErrorReference'

interface EditNotesProps {
  requestId: string
  notes: string | null
  status: RequestStatus
}

const EDIT_FAILURE_CODES: Partial<Record<string, MessageId>> = {
  invalid_notes: 'error.invalid_notes',
  start_passed: 'error.start_passed',
  invalid_state: 'error.invalid_state',
  request_not_found: 'error.request_not_found',
}

function editFailure(error: unknown): MessageId {
  const code = error instanceof ApiError ? error.code : ''
  return EDIT_FAILURE_CODES[code] ?? 'common.somethingWentWrong'
}

/** The Requester's notes editor: save or clear until the start time, which never drops a claim (spec §4.1). */
export function EditNotes({ requestId, notes, status }: EditNotesProps) {
  const edit = useEditNotes()
  const [draft, setDraft] = useState<string | null>(null)

  if (draft === null)
    return (
      <Button
        variant="outline"
        className="h-11 lg:h-8"
        onClick={() => {
          edit.reset()
          setDraft(notes ?? '')
        }}
      >
        <FormattedMessage id={notes ? 'requests.notes.edit' : 'requests.notes.add'} />
      </Button>
    )

  function save(event: SyntheticEvent) {
    event.preventDefault()
    if (draft === null) return
    edit.mutate(
      { requestId, notes: draft },
      {
        onSuccess: () => {
          setDraft(null)
        },
      },
    )
  }

  return (
    <form onSubmit={save} className="space-y-3">
      <NotesField id={`notes-${requestId}`} value={draft} onChange={setDraft} />
      {status === 'Claimed' && (
        <p className="text-sm text-muted-foreground">
          <FormattedMessage id="requests.notes.claimUnaffected" />
        </p>
      )}
      {edit.isError && (
        <Alert variant="destructive">
          <AlertDescription>
            <FormattedMessage id={editFailure(edit.error)} />
            <ErrorReference error={edit.error} />
          </AlertDescription>
        </Alert>
      )}
      <div className="flex gap-2">
        <Button type="submit" className="h-11 lg:h-8" disabled={edit.isPending}>
          <FormattedMessage id="requests.notes.save" />
        </Button>
        <Button
          type="button"
          variant="outline"
          className="h-11 lg:h-8"
          onClick={() => {
            setDraft(null)
          }}
        >
          <FormattedMessage id="requests.notes.cancel" />
        </Button>
      </div>
    </form>
  )
}
