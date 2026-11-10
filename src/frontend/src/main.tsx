import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createRouter, RouterProvider } from '@tanstack/react-router'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import { restoreTheme } from '@/features/flags'
import { installGlobalHandlers, logger } from '@/lib/logger'
import { trackRouteName } from '@/lib/logger/router'
import { restoreColorScheme } from '@/lib/theme'
import { routeTree } from './routeTree.gen'

restoreTheme()
restoreColorScheme()
installGlobalHandlers(logger)

const queryClient = new QueryClient()
const router = createRouter({ routeTree })
trackRouteName(router)

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
