import { useEffect, useRef } from 'react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { useStartSession } from './api'

interface SignInVerifyPageProps {
  token: string | undefined
  onSignedIn: () => void
}

/** Opened from the emailed link: exchanges the token for a session as soon as it loads. */
export function SignInVerifyPage({ token, onSignedIn }: SignInVerifyPageProps) {
  const start = useStartSession()
  const started = useRef(false)

  useEffect(() => {
    // The link works once, so a strict-mode double effect must not spend it twice.
    if (!token || started.current) return
    started.current = true
    start.mutate(token, { onSuccess: onSignedIn })
  }, [token, start, onSignedIn])

  if (!token || start.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>This sign-in link can&apos;t be used</AlertTitle>
        <AlertDescription>
          It may have been used already or have expired. <a href="/sign-in">Request a new one.</a>
        </AlertDescription>
      </Alert>
    )
  }
  return <p>Signing you in…</p>
}
