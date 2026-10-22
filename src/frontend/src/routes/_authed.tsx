import { createFileRoute, Link, Navigate, Outlet } from '@tanstack/react-router'
import { RequireMember } from '@/features/auth'

export const Route = createFileRoute('/_authed')({
  component: () => (
    <RequireMember signedOut={<Navigate to="/sign-in" />}>
      <nav aria-label="Main" className="flex gap-4 text-sm font-medium">
        <Link to="/credits" activeProps={{ className: 'underline' }}>
          Credits
        </Link>
        <Link to="/requests" activeProps={{ className: 'underline' }}>
          My Requests
        </Link>
      </nav>
      <Outlet />
    </RequireMember>
  ),
})
