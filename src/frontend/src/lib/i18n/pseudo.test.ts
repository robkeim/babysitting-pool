import { createIntl } from 'react-intl'
import { choosableLanguages } from './languages'
import { enUS } from './messages'
import { pseudoLocalize } from './pseudo'

const intl = createIntl({ locale: 'en-US', messages: pseudoLocalize(enUS) })

describe('pseudoLocalize', () => {
  it('accents the literal text, wraps it and pads it by the tier for its length', () => {
    expect(intl.formatMessage({ id: 'credits.loadMore' })).toBe(
      '⏩ Ļöàð ɱöŕé ·················· ⏪',
    )
  })

  it('leaves arguments and tags working', () => {
    const out = intl.formatMessage(
      { id: 'auth.invitation.joiningAs' },
      { email: 'ada@example.com', strong: (chunks) => `[${chunks.join('')}]` },
    )

    expect(out).toContain('[ada@example.com]')
    expect(out).toMatch(/^⏩ Ýöû'ŕé ĵöîñîñĝ àš/)
  })
})

describe('choosableLanguages', () => {
  it('never offers Ünïcödé Tëst in production', () => {
    expect(choosableLanguages(true).map((l) => l.tag)).toEqual(['en-US'])
    expect(choosableLanguages(false).map((l) => l.tag)).toEqual(['en-US', 'en-XA'])
  })
})
