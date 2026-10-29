import { createRootRoute, Outlet, useNavigate } from '@tanstack/react-router'
import {
  ClipboardList,
  Flag,
  HandHeart,
  HeartHandshake,
  MailPlus,
  ShieldCheck,
  Users,
  Wallet,
  Waves,
} from 'lucide-react'
import { FormattedMessage } from 'react-intl'
import { AppShell, type ShellMore, type ShellTab } from '@/components/AppShell'
import { AccountMenu, useMe } from '@/features/auth'
import { useFlagTheme } from '@/features/flags'
import { NotificationBell } from '@/features/notifications'
import { I18nProvider } from '@/lib/i18n'
import { useColorScheme } from '@/lib/theme'

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

const ADMIN_ITEMS: readonly ShellTab[] = [
  {
    to: '/admin/invitations',
    label: <FormattedMessage id="common.nav.invitations" />,
    icon: MailPlus,
  },
  { to: '/admin/members', label: <FormattedMessage id="common.nav.members" />, icon: Users },
  { to: '/admin/pool', label: <FormattedMessage id="common.nav.pool" />, icon: Waves },
  { to: '/admin/feature-flags', label: <FormattedMessage id="common.nav.flags" />, icon: Flag },
]

const ADMIN_MORE: ShellMore = {
  label: <FormattedMessage id="common.nav.admin" />,
  icon: ShieldCheck,
  items: ADMIN_ITEMS,
}

export const Route = createRootRoute({
  component: function Root() {
    const navigate = useNavigate()
    const me = useMe()
    useFlagTheme()
    useColorScheme(me.data?.colorScheme)
    return (
      <I18nProvider language={me.data?.language}>
        <AppShell
          tabs={me.data ? MEMBER_TABS : undefined}
          more={me.data?.isAdmin ? ADMIN_MORE : undefined}
          navExtra={me.data ? <NotificationBell /> : undefined}
          account={<AccountMenu onSignedOut={() => void navigate({ to: '/sign-in' })} />}
        >
          <Outlet />
        </AppShell>
      </I18nProvider>
    )
  },
})
