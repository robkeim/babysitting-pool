import { FormattedMessage, useIntl } from 'react-intl'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { type MessageId } from '@/lib/i18n'
import { COLOR_SCHEMES, type ColorScheme } from '@/lib/theme'
import { useChangeColorScheme, useMe } from './api'

const LABELS: Record<ColorScheme, MessageId> = {
  system: 'auth.colorScheme.system',
  light: 'auth.colorScheme.light',
  dark: 'auth.colorScheme.dark',
}

/** The Member's Colour Scheme, kept on the Member and not in the browser; renders nothing when nobody is signed in. */
export function ColorSchemeCard() {
  const me = useMe()
  const changeColorScheme = useChangeColorScheme()
  const intl = useIntl()
  if (!me.data) return null

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <label htmlFor="color-scheme">
            <FormattedMessage id="auth.colorScheme.title" />
          </label>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <select
          id="color-scheme"
          className="h-11 w-full rounded-lg border border-input bg-card px-2 text-base lg:h-8 lg:text-sm"
          value={me.data.colorScheme}
          disabled={changeColorScheme.isPending}
          onChange={(event) => {
            changeColorScheme.mutate(event.target.value)
          }}
        >
          {COLOR_SCHEMES.map((scheme) => (
            <option key={scheme} value={scheme}>
              {intl.formatMessage({ id: LABELS[scheme] })}
            </option>
          ))}
        </select>
      </CardContent>
    </Card>
  )
}
