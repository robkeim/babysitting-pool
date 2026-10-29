import type { IntlShape } from 'react-intl'

/** "6 hours" when the minutes are whole hours, else "90 minutes". */
export function formatDuration(intl: IntlShape, minutes: number) {
  return minutes % 60 === 0
    ? intl.formatMessage({ id: 'rules.hours' }, { count: minutes / 60 })
    : intl.formatMessage({ id: 'rules.minutes' }, { count: minutes })
}
