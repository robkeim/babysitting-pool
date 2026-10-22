/* eslint-disable formatjs/no-literal-string-in-object -- a Language is named in itself, never translated */

import type { MessageFormatElement } from '@formatjs/icu-messageformat-parser'
import { enUS, type MessageId } from './messages'

export type Messages = Record<MessageId, string> | Record<MessageId, MessageFormatElement[]>

export interface LanguageOption {
  /** The tag stored on the Member (ADR 0009). */
  tag: string
  /** The Language's name in itself, so a Member can find theirs whatever they currently read. */
  label: string
  /** The locale used to format numbers and dates. */
  formatLocale: string
  /** Offered in the dropdown only outside production. */
  testOnly: boolean
  load: () => Promise<Messages>
}

export const DEFAULT_LANGUAGE = 'en-US'

const ENGLISH: LanguageOption = {
  tag: DEFAULT_LANGUAGE,
  label: 'English (US)',
  formatLocale: 'en-US',
  testOnly: false,
  load: () => Promise.resolve(enUS),
}

/** Adding a Language is a new catalog plus one entry here (ADR 0009). */
export const LANGUAGES: readonly LanguageOption[] = [
  ENGLISH,
  {
    tag: 'en-XA',
    label: 'Ünïcödé Tëst',
    formatLocale: 'en-US',
    testOnly: true,
    load: async () => (await import('./pseudo.catalog')).default,
  },
]

export function findLanguage(tag: string | undefined): LanguageOption {
  return LANGUAGES.find((language) => language.tag === tag) ?? ENGLISH
}

/** The Languages a Member may choose between; Ünïcödé Tëst is never offered in production. */
export function choosableLanguages(production: boolean): readonly LanguageOption[] {
  return LANGUAGES.filter((language) => !(production && language.testOnly))
}
