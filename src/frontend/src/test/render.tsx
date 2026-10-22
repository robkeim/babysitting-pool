import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { IntlProvider } from 'react-intl'
import { enUS } from '@/lib/i18n'

/** Renders in English with a fresh QueryClient that never retries, so failures surface immediately. */
export function renderWithProviders(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <IntlProvider locale="en-US" messages={enUS}>
        {ui}
      </IntlProvider>
    </QueryClientProvider>,
  )
}
