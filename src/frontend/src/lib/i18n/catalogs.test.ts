/// <reference types="node" />
import { parse } from '@formatjs/icu-messageformat-parser'
import { readFileSync } from 'node:fs'
import { createIntl } from 'react-intl'
import { LANGUAGES } from './languages'
import { enUS } from './messages'

// Vitest runs from src/frontend, next to src/backend.
const problems = readFileSync('../backend/BabysittingPool.Api/Problems.cs', 'utf8')

describe('message catalogs', () => {
  it('has only valid ICU messages', () => {
    for (const [id, message] of Object.entries(enUS)) {
      expect(() => parse(message), id).not.toThrow()
    }
  })

  it('has an error.<code> message for every backend problem code (ADR 0009)', () => {
    const codes = [...problems.matchAll(/StatusCodes\.\w+,\s*"([a-z_]+)"/g)].map(
      (match) => match[1] ?? '',
    )

    expect(codes.length).toBeGreaterThan(0)
    for (const code of codes) expect(enUS, code).toHaveProperty(`error.${code}`)
  })

  it('gives every Language exactly the English keys', async () => {
    for (const language of LANGUAGES) {
      const messages = await language.load()
      expect(Object.keys(messages).sort(), language.tag).toEqual(Object.keys(enUS).sort())
    }
  })

  it('pluralizes credit amounts: 1.00 credit, 0.25 credits', async () => {
    const english = createIntl({ locale: 'en-US', messages: enUS })
    const id = 'requests.post.receipt.amount'
    expect(english.formatMessage({ id }, { amount: 1 })).toBe('1.00 credit')
    expect(english.formatMessage({ id }, { amount: 0.25 })).toBe('0.25 credits')
    expect(english.formatMessage({ id }, { amount: 10.5 })).toBe('10.50 credits')

    const french = createIntl({
      locale: 'fr-FR',
      messages: await (LANGUAGES.find((language) => language.tag === 'fr-FR')?.load() ?? enUS),
    })
    expect(french.formatMessage({ id: 'requests.post.receipt.after' }, { after: 0.25 })).toBe(
      'Après publication : 0,25 crédit restant',
    )
  })
})
