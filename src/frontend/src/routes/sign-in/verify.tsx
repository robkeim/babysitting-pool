import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { SignInVerifyPage } from '@/features/auth'

export const Route = createFileRoute('/sign-in/verify')({
  validateSearch: (search: Record<string, unknown>) => ({
    token: typeof search.token === 'string' ? search.token : undefined,
  }),
  component: function VerifyRoute() {
    const { token } = Route.useSearch()
    const navigate = useNavigate()
    return (
      <SignInVerifyPage
        token={token}
        onSignedIn={() => void navigate({ to: '/credits', replace: true })}
      />
    )
  },
})
