import { CircleHelp, LogOut } from 'lucide-react'
import { useIntl } from 'react-intl'
import { Button, buttonVariants } from '@/components/ui/button'
import { useMe, useSignOut } from './api'

interface AccountMenuProps {
  onSignedOut: () => void
}

/** Who is signed in, links to their profile and help, and a sign-out button; renders nothing when nobody is. */
export function AccountMenu({ onSignedOut }: AccountMenuProps) {
  const intl = useIntl()
  const me = useMe()
  const signOut = useSignOut()
  if (!me.data) return null

  return (
    <div className="flex items-center gap-2 text-sm">
      <span className="sr-only">{me.data.displayName}</span>
      <a
        href="/profile"
        aria-label={intl.formatMessage({ id: 'auth.accountMenu.profile' })}
        title={me.data.displayName}
        className="grid size-9 shrink-0 place-items-center rounded-full bg-accent font-semibold text-accent-foreground uppercase transition-colors hover:bg-accent/80"
      >
        {Array.from(me.data.displayName)[0]}
      </a>
      <a
        href="/how-it-works"
        aria-label={intl.formatMessage({ id: 'auth.accountMenu.howItWorks' })}
        className={buttonVariants({ variant: 'ghost', size: 'icon-lg' })}
      >
        <CircleHelp />
      </a>
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
