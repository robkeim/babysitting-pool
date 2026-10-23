import { ApiError } from '@/lib/api/client'
import { type MessageId } from '@/lib/i18n'

const RELEASE_FAILURE_CODES: Partial<Record<string, MessageId>> = {
  invalid_state: 'error.invalid_state',
  start_passed: 'error.start_passed',
  request_not_found: 'error.request_not_found',
  concurrent_update: 'error.invalid_state',
}

/** The message for a rejected Cancel or Withdraw, never the server's own text. */
export function releaseFailure(error: unknown): MessageId {
  const code = error instanceof ApiError ? error.code : ''
  return RELEASE_FAILURE_CODES[code] ?? 'common.somethingWentWrong'
}
