import {
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
  Outlet,
  RouterProvider,
} from '@tanstack/react-router'
import { render, screen } from '@testing-library/react'
import { IntlProvider } from 'react-intl'
import { ErrorBoundary } from '@/components/ErrorBoundary'
import { enUS } from '@/lib/i18n'
import { getRouteName, logger } from './index'
import { trackRouteName } from './router'

function Thrower(): never {
  throw new Error('Render failed')
}

function routerFor(path: string) {
  // As in routes/__root.tsx: a boundary around the outlet, which is where a route's render error lands.
  const root = createRootRoute({
    component: () => (
      <ErrorBoundary>
        <Outlet />
      </ErrorBoundary>
    ),
  })
  const broken = createRoute({ getParentRoute: () => root, path: '/broken', component: Thrower })
  const item = createRoute({
    getParentRoute: () => root,
    path: '/items/$itemId',
    component: () => <p>Item</p>,
  })
  return createRouter({
    routeTree: root.addChildren([broken, item]),
    history: createMemoryHistory({ initialEntries: [path] }),
  })
}

describe('router logging', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('reports a route component render error once, through the root boundary', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const reportError = vi.spyOn(logger, 'reportError')

    render(
      <IntlProvider locale="en-US" messages={enUS}>
        <RouterProvider router={routerFor('/broken')} />
      </IntlProvider>,
    )

    await screen.findByText('Something went wrong.')
    expect(reportError).toHaveBeenCalledOnce()
    expect(reportError).toHaveBeenCalledWith(
      expect.objectContaining({ message: 'Render failed' }),
      {
        code: 'render_error',
      },
    )
  })

  it('names the route by its id, never the concrete path', async () => {
    const router = routerFor('/items/secret-id')
    const stop = trackRouteName(router)

    render(<RouterProvider router={router} />)
    await screen.findByText('Item')

    expect(getRouteName()).toBe('/items/$itemId')
    stop()
  })
})
