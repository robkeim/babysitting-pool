import { Button } from '@/components/ui/button'
import { useMe, useSignOut } from './api'

interface AccountMenuProps {
  onSignedOut: () => void
}

/** Who is signed in, with a sign-out button; renders nothing when nobody is. */
export function AccountMenu({ onSignedOut }: AccountMenuProps) {
  const me = useMe()
  const signOut = useSignOut()
  if (!me.data) return null

  return (
    <div className="flex items-center gap-3 text-sm">
      <span>{me.data.displayName}</span>
      <Button
        variant="outline"
        disabled={signOut.isPending}
        onClick={() => {
          signOut.mutate(undefined, { onSuccess: onSignedOut })
        }}
      >
        Sign out
      </Button>
    </div>
  )
}
