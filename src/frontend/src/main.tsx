import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createRouter, RouterProvider } from '@tanstack/react-router'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import { restoreTheme } from '@/features/flags'
import { installGlobalHandlers, logger, setRouteName } from '@/lib/logger'
import { restoreColorScheme } from '@/lib/theme'
import { routeTree } from './routeTree.gen'

restoreTheme()
restoreColorScheme()
installGlobalHandlers(logger)

const queryClient = new QueryClient()
// A route component's render error is caught by the router before any boundary of ours (ADR 0010).
const router = createRouter({
  routeTree,
  defaultOnCatch: (error) => {
    logger.reportError(error, { code: 'render_error' })
  },
})
router.subscribe('onResolved', () => {
  // The route's id (`/_authed/requests/$requestId`), never the concrete path, which carries ids.
  setRouteName(router.state.matches.at(-1)?.routeId ?? 'unknown')
})

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}

const rootElement = document.getElementById('root')
if (!rootElement) throw new Error('Missing #root element')

createRoot(rootElement).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
)
