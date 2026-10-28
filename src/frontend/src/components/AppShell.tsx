import { Menu } from '@base-ui/react/menu'
import { Link, type LinkProps, useMatchRoute } from '@tanstack/react-router'
import { HeartHandshake, type LucideIcon } from 'lucide-react'
import { type ReactNode, useEffect, useRef, useState, useSyncExternalStore } from 'react'
import { FormattedMessage, useIntl } from 'react-intl'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from '@/components/ui/sheet'

export interface ShellTab {
  to: LinkProps['to']
  label: ReactNode
  icon: LucideIcon
}

/** Secondary destinations behind one tab: a sheet below `lg`, a dropdown from `lg` up. */
export interface ShellMore {
  label: ReactNode
  icon: LucideIcon
  items: readonly ShellTab[]
}

interface AppShellProps {
  /** Rendered as the navigation when present: a bottom bar below `lg`, in the top bar from `lg` up. */
  tabs?: readonly ShellTab[]
  more?: ShellMore
  /** Controls that are not links, such as the notification bell, shown after the tabs in the same navigation. */
  navExtra?: ReactNode
  account?: ReactNode
  children: ReactNode
}

/** How one item in the navigation looks: a labelled icon in the bottom bar, an icon and label in the top bar.
 * Labels wrap between words and never inside one; a single word too long for a tab on mobile is clipped, not split,
 * and from `lg` up a tab is never narrower than its longest word.
 */
export const shellTabClass =
  'flex min-w-0 flex-1 flex-col items-center gap-1 px-1 py-2 text-center text-[0.6875rem] leading-tight font-medium text-muted-foreground transition-colors hover:text-foreground data-[status=active]:text-primary lg:min-w-min lg:flex-initial lg:flex-row lg:gap-2 lg:rounded-lg lg:px-3 lg:text-sm lg:data-[status=active]:bg-accent'
const TAB_LABEL_CLASS = 'line-clamp-2 min-w-0 lg:min-w-min'

const DESKTOP_QUERY = '(min-width: 64rem)'

function useIsDesktop() {
  return useSyncExternalStore(
    (notify) => {
      const query = window.matchMedia(DESKTOP_QUERY)
      query.addEventListener('change', notify)
      return () => {
        query.removeEventListener('change', notify)
      }
    },
    () => window.matchMedia(DESKTOP_QUERY).matches,
    () => false,
  )
}

function MoreTab({ label, icon: Icon, items }: ShellMore) {
  const isDesktop = useIsDesktop()
  const matchRoute = useMatchRoute()
  const [open, setOpen] = useState(false)
  const active = items.some(({ to }) => matchRoute({ to, fuzzy: true }) !== false)
  const trigger = (
    <>
      <Icon className="size-5 shrink-0 lg:size-4" aria-hidden />
      <span className={TAB_LABEL_CLASS}>{label}</span>
    </>
  )
  const triggerClass = `${shellTabClass} ${active ? 'text-primary lg:bg-accent' : ''}`

  if (isDesktop) {
    return (
      <DropdownMenu>
        <DropdownMenuTrigger className={triggerClass}>{trigger}</DropdownMenuTrigger>
        <DropdownMenuContent>
          {items.map(({ to, label: itemLabel, icon: ItemIcon }) => (
            <Menu.Item
              key={to}
              render={<Link to={to} />}
              className="flex items-center gap-2 rounded-lg px-3 py-2 outline-none data-highlighted:bg-accent data-[status=active]:text-primary"
            >
              <ItemIcon className="size-4 shrink-0" aria-hidden />
              <span>{itemLabel}</span>
            </Menu.Item>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>
    )
  }

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger className={triggerClass}>{trigger}</SheetTrigger>
      <SheetContent>
        <SheetTitle>{label}</SheetTitle>
        {items.map(({ to, label: itemLabel, icon: ItemIcon }) => (
          <Link
            key={to}
            to={to}
            onClick={() => {
              setOpen(false)
            }}
            className="flex min-h-11 items-center gap-3 rounded-lg px-2 hover:bg-accent data-[status=active]:text-primary"
          >
            <ItemIcon className="size-5 shrink-0" aria-hidden />
            <span className="min-w-0">{itemLabel}</span>
          </Link>
        ))}
      </SheetContent>
    </Sheet>
  )
}

/** The frame every page sits in: a top bar with the wordmark and account, tab navigation, one content column. */
export function AppShell({ tabs, more, navExtra, account, children }: AppShellProps) {
  const intl = useIntl()
  const shell = useRef<HTMLDivElement>(null)
  const nav = useRef<HTMLElement>(null)
  const hasTabs = tabs !== undefined

  // The bottom bar is fixed and as tall as its longest wrapped label, so the page reserves what it measures
  // (nothing from `lg`, where the bar is part of the top bar) instead of guessing.
  useEffect(() => {
    const element = nav.current
    const container = shell.current
    if (!element || !container) return
    const reserve = () => {
      const height = getComputedStyle(element).position === 'fixed' ? element.offsetHeight : 0
      container.style.setProperty('--tabbar-height', `${height}px`)
    }
    reserve()
    const observer = new ResizeObserver(reserve)
    observer.observe(element)
    window.addEventListener('resize', reserve)
    return () => {
      observer.disconnect()
      window.removeEventListener('resize', reserve)
    }
  }, [hasTabs])

  return (
    <div ref={shell} className="flex min-h-dvh flex-col">
      <header className="sticky top-0 z-20 border-b bg-card">
        <div className="mx-auto flex min-h-14 max-w-6xl items-center gap-3 px-4 lg:gap-6">
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
              ref={nav}
              aria-label={intl.formatMessage({ id: 'common.nav.label' })}
              className="fixed inset-x-0 bottom-0 z-20 flex border-t bg-card pb-[env(safe-area-inset-bottom)] lg:static lg:min-w-0 lg:flex-1 lg:border-t-0 lg:pb-0"
            >
              {tabs.map(({ to, label, icon: Icon }) => (
                <Link key={to} to={to} className={shellTabClass}>
                  <Icon className="size-5 shrink-0 lg:size-4" aria-hidden />
                  <span className={TAB_LABEL_CLASS}>{label}</span>
                </Link>
              ))}
              {more && <MoreTab {...more} />}
              {navExtra}
            </nav>
          )}
          {account && <div className="ml-auto shrink-0">{account}</div>}
        </div>
      </header>
      <main className="mx-auto w-full max-w-3xl flex-1 space-y-4 px-4 pt-6 pb-[calc(var(--tabbar-height,0px)+2rem)]">
        {children}
      </main>
    </div>
  )
}
