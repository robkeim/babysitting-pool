import { useIntl } from 'react-intl'
import { Button } from '@/components/ui/button'
import { choosableLanguages } from '@/lib/i18n'
import { useChangeLanguage, useMe, useSignOut } from './api'

interface AccountMenuProps {
  onSignedOut: () => void
}

/** Who is signed in, their Language and a sign-out button; renders nothing when nobody is. */
export function AccountMenu({ onSignedOut }: AccountMenuProps) {
  const intl = useIntl()
  const me = useMe()
  const signOut = useSignOut()
  const changeLanguage = useChangeLanguage()
  if (!me.data) return null

  return (
    <div className="flex items-center gap-3 text-sm">
      <span>{me.data.displayName}</span>
      <select
        aria-label={intl.formatMessage({ id: 'auth.accountMenu.language' })}
        className="h-8 rounded-lg border border-input bg-background px-2"
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
      <Button
        variant="outline"
        disabled={signOut.isPending}
        onClick={() => {
          signOut.mutate(undefined, { onSuccess: onSignedOut })
        }}
      >
        {intl.formatMessage({ id: 'auth.accountMenu.signOut' })}
      </Button>
    </div>
  )
}
