import { useState, type SyntheticEvent } from 'react'
import { FormattedMessage } from 'react-intl'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { useRequestSignInLink } from './api'
import { ErrorReference } from '@/components/ErrorReference'

/** Asks for an email address and says, whatever it is, that a link was sent if it belongs to a Member. */
export function SignInPage() {
  const [email, setEmail] = useState('')
  const request = useRequestSignInLink()

  function submit(event: SyntheticEvent) {
    event.preventDefault()
    request.mutate(email.trim())
  }

  return (
    <Card className="mx-auto w-full max-w-sm lg:mt-10">
      <CardHeader>
        <CardTitle className="text-xl font-semibold">
          <FormattedMessage id="auth.signIn.title" />
        </CardTitle>
        <p className="text-sm text-muted-foreground">
          <FormattedMessage id="auth.signIn.tagline" />
        </p>
      </CardHeader>
      <CardContent className="space-y-4">
        {request.isSuccess ? (
          <Alert>
            <AlertTitle>
              <FormattedMessage id="auth.signIn.sent.title" />
            </AlertTitle>
            <AlertDescription>
              <FormattedMessage id="auth.signIn.sent.body" />
            </AlertDescription>
          </Alert>
        ) : (
          <form onSubmit={submit} className="space-y-4">
            <div className="space-y-2">
              <label htmlFor="email" className="text-sm font-medium">
                <FormattedMessage id="auth.signIn.email" />
              </label>
              <Input
                id="email"
                className="h-11 lg:h-9"
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
                <AlertTitle>
                  <FormattedMessage id="auth.signIn.invalid.title" />
                </AlertTitle>
                <AlertDescription>
                  <FormattedMessage id="auth.signIn.invalid.body" />
                  <ErrorReference error={request.error} />
                </AlertDescription>
              </Alert>
            )}
            <Button type="submit" className="h-11 w-full lg:h-9" disabled={request.isPending}>
              <FormattedMessage id="auth.signIn.submit" />
            </Button>
          </form>
        )}
      </CardContent>
    </Card>
  )
}
