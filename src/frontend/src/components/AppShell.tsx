import { Link, type LinkProps } from '@tanstack/react-router'
import { HeartHandshake, type LucideIcon } from 'lucide-react'
import { type ReactNode } from 'react'
import { FormattedMessage, useIntl } from 'react-intl'

export interface ShellTab {
  to: LinkProps['to']
  label: ReactNode
  icon: LucideIcon
}

interface AppShellProps {
  /** Rendered as the navigation when present: a bottom bar below `lg`, in the top bar from `lg` up. */
  tabs?: readonly ShellTab[]
  account?: ReactNode
  children: ReactNode
}

/** The frame every page sits in: a top bar with the wordmark and account, tab navigation, one content column. */
export function AppShell({ tabs, account, children }: AppShellProps) {
  const intl = useIntl()
  return (
    <div className="flex min-h-dvh flex-col">
      <header className="sticky top-0 z-20 border-b bg-card">
        <div className="mx-auto flex min-h-14 max-w-5xl items-center gap-3 px-4 lg:gap-6">
          <h1 className="flex min-w-0 items-center gap-2 text-base font-semibold tracking-tight">
            <span
              aria-hidden
              className="grid size-8 shrink-0 place-items-center rounded-lg bg-primary text-primary-foreground"
            >
              <HeartHandshake className="size-5" />
            </span>
            <span className="min-w-0 [overflow-wrap:anywhere]">
              <FormattedMessage id="common.appName" />
            </span>
          </h1>
          {tabs && (
            <nav
              aria-label={intl.formatMessage({ id: 'common.nav.label' })}
              className="fixed inset-x-0 bottom-0 z-20 flex border-t bg-card pb-[env(safe-area-inset-bottom)] lg:static lg:min-w-0 lg:flex-1 lg:border-t-0 lg:pb-0"
            >
              {tabs.map(({ to, label, icon: Icon }) => (
                <Link
                  key={to}
                  to={to}
                  className="flex min-w-0 flex-1 flex-col items-center gap-1 px-1 py-2 text-center text-[0.6875rem] leading-tight font-medium text-muted-foreground transition-colors hover:text-foreground data-[status=active]:text-primary lg:flex-initial lg:flex-row lg:gap-2 lg:rounded-lg lg:px-3 lg:text-sm lg:data-[status=active]:bg-accent"
                >
                  <Icon className="size-5 shrink-0 lg:size-4" aria-hidden />
                  <span className="[overflow-wrap:anywhere]">{label}</span>
                </Link>
              ))}
            </nav>
          )}
          {account && <div className="ml-auto shrink-0">{account}</div>}
        </div>
      </header>
      <main className="mx-auto w-full max-w-3xl flex-1 space-y-4 px-4 py-6 pb-28 lg:pb-10">
        {children}
      </main>
    </div>
  )
}
