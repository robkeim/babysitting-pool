import { createFileRoute, Link, Navigate, Outlet } from '@tanstack/react-router'
import { FormattedMessage, useIntl } from 'react-intl'
import { RequireMember } from '@/features/auth'

export const Route = createFileRoute('/_authed')({
  component: AuthedLayout,
})

function AuthedLayout() {
  const intl = useIntl()
  return (
    <RequireMember signedOut={<Navigate to="/sign-in" />}>
      <nav
        aria-label={intl.formatMessage({ id: 'common.nav.label' })}
        className="flex gap-4 text-sm font-medium"
      >
        <Link to="/credits" activeProps={{ className: 'underline' }}>
          <FormattedMessage id="common.nav.credits" />
        </Link>
        <Link to="/requests" activeProps={{ className: 'underline' }}>
          <FormattedMessage id="requests.myRequests.title" />
        </Link>
      </nav>
      <Outlet />
    </RequireMember>
  )
}
