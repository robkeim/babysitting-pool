import { keepPreviousData, useQuery } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { IntlProvider } from 'react-intl'
import { DEFAULT_LANGUAGE, findLanguage } from './languages'
import { enUS } from './messages'

interface I18nProviderProps {
  /** The signed-in Member's Language tag; English until there is one. */
  language: string | undefined
  children: ReactNode
}

/** Shows the app in one Language; a lazy catalog loads in the background while the previous one stays up. */
export function I18nProvider({ language, children }: I18nProviderProps) {
  const option = findLanguage(language)
  const catalog = useQuery({
    queryKey: ['i18n', 'catalog', option.tag],
    queryFn: option.load,
    staleTime: Infinity,
    placeholderData: keepPreviousData,
  })

  return (
    <IntlProvider
      locale={option.formatLocale}
      defaultLocale={DEFAULT_LANGUAGE}
      messages={catalog.data ?? enUS}
    >
      {children}
    </IntlProvider>
  )
}
