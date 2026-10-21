import { createRootRoute, Outlet } from '@tanstack/react-router'

export const Route = createRootRoute({
  component: () => (
    <main className="mx-auto max-w-2xl space-y-6 p-8">
      <h1 className="text-3xl font-bold">Babysitting Pool</h1>
      <Outlet />
    </main>
  ),
})
