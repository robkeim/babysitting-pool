import { FormattedMessage } from 'react-intl'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { choosableLanguages } from '@/lib/i18n'
import { useChangeLanguage, useMe } from './api'

/** The Member's Language, kept on the Member and not in the browser (ADR 0009); renders nothing when nobody is signed in. */
export function LanguageCard() {
  const me = useMe()
  const changeLanguage = useChangeLanguage()
  if (!me.data) return null

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <label htmlFor="language">
            <FormattedMessage id="auth.language.title" />
          </label>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <select
          id="language"
          className="h-11 w-full rounded-lg border border-input bg-card px-2 text-base lg:h-8 lg:text-sm"
          value={me.data.language}
          disabled={changeLanguage.isPending}
          onChange={(event) => {
            changeLanguage.mutate(event.target.value)
          }}
        >
          {choosableLanguages(import.meta.env.PROD).map((language) => (
            <option key={language.tag} value={language.tag}>
              {language.label}
            </option>
          ))}
        </select>
      </CardContent>
    </Card>
  )
}
