import { createRootRoute, Outlet, useNavigate } from '@tanstack/react-router'
import { AccountMenu } from '@/features/auth'

export const Route = createRootRoute({
  component: function Root() {
    const navigate = useNavigate()
    return (
      <main className="mx-auto max-w-2xl space-y-6 p-8">
        <header className="flex items-center justify-between">
          <h1 className="text-3xl font-bold">Babysitting Pool</h1>
          <AccountMenu onSignedOut={() => void navigate({ to: '/sign-in' })} />
        </header>
        <Outlet />
      </main>
    )
  },
})
