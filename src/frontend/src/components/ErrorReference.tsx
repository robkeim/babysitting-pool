import { FormattedMessage } from 'react-intl'
import { ApiError } from '@/lib/api/client'

/** The reference (the request's trace id) of a failed call, for a Member to quote; nothing for other errors. */
export function ErrorReference({ error }: { error: unknown }) {
  if (!(error instanceof ApiError)) return null
  return (
    <span className="mt-1 block text-xs text-muted-foreground">
      <FormattedMessage
        id="common.errorReference"
        values={{ reference: <code className="font-mono select-all">{error.traceId}</code> }}
      />
    </span>
  )
}
