import { createRootRoute, Outlet, useNavigate } from '@tanstack/react-router'
import { FormattedMessage } from 'react-intl'
import { AccountMenu, useMe } from '@/features/auth'
import { I18nProvider } from '@/lib/i18n'

export const Route = createRootRoute({
  component: function Root() {
    const navigate = useNavigate()
    const me = useMe()
    return (
      <I18nProvider language={me.data?.language}>
        <main className="mx-auto max-w-2xl space-y-6 p-8">
          <header className="flex items-center justify-between">
            <h1 className="text-3xl font-bold">
              <FormattedMessage id="common.appName" />
            </h1>
            <AccountMenu onSignedOut={() => void navigate({ to: '/sign-in' })} />
          </header>
          <Outlet />
        </main>
      </I18nProvider>
    )
  },
})
