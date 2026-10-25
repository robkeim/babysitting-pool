import { createRootRoute, Outlet, useNavigate } from '@tanstack/react-router'
import { ClipboardList, Flag, HandHeart, HeartHandshake, Wallet } from 'lucide-react'
import { FormattedMessage } from 'react-intl'
import { AppShell, type ShellTab } from '@/components/AppShell'
import { AccountMenu, useMe } from '@/features/auth'
import { I18nProvider } from '@/lib/i18n'

const MEMBER_TABS: readonly ShellTab[] = [
  { to: '/credits', label: <FormattedMessage id="common.nav.credits" />, icon: Wallet },
  {
    to: '/requests',
    label: <FormattedMessage id="requests.myRequests.title" />,
    icon: ClipboardList,
  },
  { to: '/open-requests', label: <FormattedMessage id="requests.open.title" />, icon: HandHeart },
  { to: '/claims', label: <FormattedMessage id="requests.claims.title" />, icon: HeartHandshake },
]

const ADMIN_TABS: readonly ShellTab[] = [
  { to: '/admin/feature-flags', label: <FormattedMessage id="common.nav.flags" />, icon: Flag },
]

export const Route = createRootRoute({
  component: function Root() {
    const navigate = useNavigate()
    const me = useMe()
    return (
      <I18nProvider language={me.data?.language}>
        <AppShell
          tabs={
            me.data ? (me.data.isAdmin ? [...MEMBER_TABS, ...ADMIN_TABS] : MEMBER_TABS) : undefined
          }
          account={<AccountMenu onSignedOut={() => void navigate({ to: '/sign-in' })} />}
        >
          <Outlet />
        </AppShell>
      </I18nProvider>
    )
  },
})
