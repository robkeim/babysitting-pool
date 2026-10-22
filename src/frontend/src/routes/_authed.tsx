import { createFileRoute, Navigate, Outlet } from '@tanstack/react-router'
import { RequireMember } from '@/features/auth'

export const Route = createFileRoute('/_authed')({
  component: () => (
    <RequireMember signedOut={<Navigate to="/sign-in" />}>
      <Outlet />
    </RequireMember>
  ),
})
