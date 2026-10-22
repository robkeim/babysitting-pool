import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { InvitationPage } from '@/features/auth'

export const Route = createFileRoute('/invitation')({
  validateSearch: (search: Record<string, unknown>) => ({
    token: typeof search.token === 'string' ? search.token : undefined,
  }),
  component: function InvitationRoute() {
    const { token } = Route.useSearch()
    const navigate = useNavigate()
    return (
      <InvitationPage
        token={token}
        onJoined={() => void navigate({ to: '/credits', replace: true })}
      />
    )
  },
})
