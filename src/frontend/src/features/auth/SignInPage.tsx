import { useState, type SyntheticEvent } from 'react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { useRequestSignInLink } from './api'

/** Asks for an email address and says, whatever it is, that a link was sent if it belongs to a Member. */
export function SignInPage() {
  const [email, setEmail] = useState('')
  const request = useRequestSignInLink()

  function submit(event: SyntheticEvent) {
    event.preventDefault()
    request.mutate(email.trim())
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Sign in</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {request.isSuccess ? (
          <Alert>
            <AlertTitle>Check your email</AlertTitle>
            <AlertDescription>
              If that address belongs to a Member, we&apos;ve sent a sign-in link. It works once and
              expires in a day.
            </AlertDescription>
          </Alert>
        ) : (
          <form onSubmit={submit} className="space-y-4">
            <div className="space-y-2">
              <label htmlFor="email" className="text-sm font-medium">
                Email address
              </label>
              <Input
                id="email"
                type="email"
                autoComplete="email"
                required
                value={email}
                onChange={(event) => {
                  setEmail(event.target.value)
                }}
              />
            </div>
            {request.isError && (
              <Alert variant="destructive">
                <AlertTitle>That doesn&apos;t look like an email address</AlertTitle>
                <AlertDescription>Please check it and try again.</AlertDescription>
              </Alert>
            )}
            <Button type="submit" disabled={request.isPending}>
              Email me a sign-in link
            </Button>
          </form>
        )}
      </CardContent>
    </Card>
  )
}
