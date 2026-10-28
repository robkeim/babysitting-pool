import {
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
  RouterProvider,
} from '@tanstack/react-router'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Flag, Users, Wallet } from 'lucide-react'
import { IntlProvider } from 'react-intl'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { enUS } from '@/lib/i18n'
import { AppShell, type ShellMore, type ShellTab } from './AppShell'

const tabs: readonly ShellTab[] = [{ to: '/credits', label: 'Credits', icon: Wallet }]
const more: ShellMore = {
  label: 'Admin',
  icon: Users,
  items: [
    { to: '/admin/members', label: 'Members', icon: Users },
    { to: '/admin/feature-flags', label: 'Feature flags', icon: Flag },
  ],
}

function stubViewport(desktop: boolean) {
  vi.stubGlobal(
    'ResizeObserver',
    class {
      observe = () => undefined
      disconnect = () => undefined
    },
  )
  vi.stubGlobal('matchMedia', () => ({
    matches: desktop,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
  }))
}

async function renderShell(path: string) {
  const root = createRootRoute({
    component: () => (
      <IntlProvider locale="en-US" messages={enUS}>
        <AppShell tabs={tabs} more={more}>
          <p>page</p>
        </AppShell>
      </IntlProvider>
    ),
  })
  const routes = ['/credits', '/admin/members', '/admin/feature-flags'].map((to) =>
    createRoute({ getParentRoute: () => root, path: to }),
  )
  const router = createRouter({
    routeTree: root.addChildren(routes),
    history: createMemoryHistory({ initialEntries: [path] }),
  })
  render(<RouterProvider router={router} />)
  await screen.findByText('page')
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('AppShell More tab', () => {
  it('opens a sheet of the extra destinations below lg and closes it on navigation', async () => {
    stubViewport(false)
    await renderShell('/credits')

    await userEvent.click(screen.getByRole('button', { name: 'Admin' }))
    await userEvent.click(await screen.findByRole('link', { name: 'Members' }))

    expect(screen.queryByRole('link', { name: 'Feature flags' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Admin' })).toHaveClass('text-primary')
  })

  it('opens a dropdown menu from lg up', async () => {
    stubViewport(true)
    await renderShell('/credits')

    await userEvent.click(screen.getByRole('button', { name: 'Admin' }))

    expect(await screen.findByRole('menuitem', { name: 'Feature flags' })).toBeInTheDocument()
  })
})
