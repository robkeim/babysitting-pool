import { FormattedMessage } from 'react-intl'
import { Textarea } from '@/components/ui/textarea'
import { NOTES_MAX_LENGTH } from './notes'

interface NotesFieldProps {
  id: string
  value: string
  onChange: (value: string) => void
}

/** The "Notes for the sitter" textarea with its character counter, shared by the post form and the editor. */
export function NotesField({ id, value, onChange }: NotesFieldProps) {
  return (
    <div className="space-y-2">
      <label htmlFor={id} className="text-sm font-medium">
        <FormattedMessage id="requests.notes.label" />
      </label>
      <Textarea
        id={id}
        value={value}
        maxLength={NOTES_MAX_LENGTH}
        aria-describedby={`${id}-count`}
        onChange={(event) => {
          onChange(event.target.value)
        }}
      />
      <p id={`${id}-count`} className="text-sm text-muted-foreground">
        <FormattedMessage
          id="requests.notes.count"
          values={{ count: value.length, max: NOTES_MAX_LENGTH }}
        />
      </p>
    </div>
  )
}
