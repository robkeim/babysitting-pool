import { CircleHelp, LogOut, UserRound } from 'lucide-react'
import { useIntl } from 'react-intl'
import { Button, buttonVariants } from '@/components/ui/button'
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
    <div className="flex items-center gap-2 text-sm">
      <span
        aria-hidden
        title={me.data.displayName}
        className="hidden size-8 shrink-0 place-items-center rounded-full bg-accent font-semibold text-accent-foreground uppercase sm:grid"
      >
        {Array.from(me.data.displayName)[0]}
      </span>
      <span className="sr-only">{me.data.displayName}</span>
      <a
        href="/profile"
        aria-label={intl.formatMessage({ id: 'auth.accountMenu.profile' })}
        className={buttonVariants({ variant: 'ghost', size: 'icon-lg' })}
      >
        <UserRound />
      </a>
      <a
        href="/how-it-works"
        aria-label={intl.formatMessage({ id: 'auth.accountMenu.howItWorks' })}
        className={buttonVariants({ variant: 'ghost', size: 'icon-lg' })}
      >
        <CircleHelp />
      </a>
      <select
        aria-label={intl.formatMessage({ id: 'auth.accountMenu.language' })}
        className="h-9 max-w-32 rounded-lg border border-input bg-card px-2 text-base lg:max-w-none lg:text-sm"
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
        variant="ghost"
        size="icon-lg"
        aria-label={intl.formatMessage({ id: 'auth.accountMenu.signOut' })}
        disabled={signOut.isPending}
        onClick={() => {
          signOut.mutate(undefined, { onSuccess: onSignedOut })
        }}
      >
        <LogOut />
      </Button>
    </div>
  )
}
