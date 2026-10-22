import { parse, TYPE, type MessageFormatElement } from '@formatjs/icu-messageformat-parser'

/** Look-alike variants per letter; each occurrence takes the next one so every variant gets exercised. */
const ACCENTS: Record<string, string> = {
  a: 'áàäâãåāăąǎ',
  b: 'ƀɓḃḅƃ',
  c: 'çćĉċčƈ',
  d: 'ðďđḋḍɗ',
  e: 'éèëêēĕėęěȅ',
  f: 'ƒḟƒ',
  g: 'ĝğġģǵǧ',
  h: 'ĥħḣḥȟ',
  i: 'íìïîĩīĭįǐ',
  j: 'ĵǰɉ',
  k: 'ķƙḱǩ',
  l: 'ĺļľŀłḷ',
  m: 'ḿṁṃɱ',
  n: 'ñńņňŉṅṇ',
  o: 'óòöôõōŏőǒø',
  p: 'þṕṗƥ',
  q: 'ɋʠǫ',
  r: 'ŕŗřṙṛ',
  s: 'śŝşšșṡ',
  t: 'ţťŧṫṭț',
  u: 'úùüûũūŭůűųǔ',
  v: 'ṽṿʋ',
  w: 'ŵẁẃẅẇ',
  x: 'ẋẍ',
  y: 'ýÿŷẏỳ',
  z: 'źżžẑẓƶ',
  A: 'ÁÀÄÂÃÅĀĂĄǍ',
  B: 'ƁḂḄɃ',
  C: 'ÇĆĈĊČƇ',
  D: 'ÐĎĐḊḌƊ',
  E: 'ÉÈËÊĒĔĖĘĚȄ',
  F: 'ƑḞ',
  G: 'ĜĞĠĢǴǦ',
  H: 'ĤĦḢḤȞ',
  I: 'ÍÌÏÎĨĪĬĮǏ',
  J: 'ĴɈ',
  K: 'ĶƘḰǨ',
  L: 'ĹĻĽĿŁḶ',
  M: 'ḾṀṂ',
  N: 'ÑŃŅŇṄṆ',
  O: 'ÓÒÖÔÕŌŎŐǑØ',
  P: 'ÞṔṖƤ',
  Q: 'ɊǪ',
  R: 'ŔŖŘṘṚ',
  S: 'ŚŜŞŠȘṠ',
  T: 'ŢŤŦṪṬȚ',
  U: 'ÚÙÜÛŨŪŬŮŰŲǓ',
  V: 'ṼṾƲ',
  W: 'ŴẀẂẄẆ',
  X: 'ẊẌ',
  Y: 'ÝŶŸẎỲ',
  Z: 'ŹŻŽẐẒƵ',
}

/**
 * Added length by English message length, from the upper end of the W3C/IBM text-expansion averages so the
 * pseudo-language stresses layouts instead of averaging them (ADR 0009).
 */
const PADDING_TIERS: readonly { upTo: number; expansion: number }[] = [
  { upTo: 10, expansion: 2 },
  { upTo: 20, expansion: 1 },
  { upTo: 30, expansion: 0.8 },
  { upTo: 70, expansion: 0.6 },
  { upTo: Infinity, expansion: 0.3 },
]

const PAD = '·'

function accent(text: string): string {
  return Array.from(text, (character, index) => {
    const variants = Array.from(ACCENTS[character] ?? '')
    // Position, not chance: the output depends only on the English string (ADR 0009).
    return variants[index % variants.length] ?? character
  }).join('')
}

function padding(source: string): string {
  const tier = PADDING_TIERS.find((candidate) => source.length <= candidate.upTo)
  return PAD.repeat(Math.ceil(source.length * (tier?.expansion ?? 0)))
}

/** Rewrites only literal text; arguments, plurals and tags keep their shape so the message still formats. */
function accentElements(elements: MessageFormatElement[]): MessageFormatElement[] {
  return elements.map((element): MessageFormatElement => {
    switch (element.type) {
      case TYPE.literal:
        return { ...element, value: accent(element.value) }
      case TYPE.tag:
        return { ...element, children: accentElements(element.children) }
      case TYPE.select:
      case TYPE.plural:
        return {
          ...element,
          options: Object.fromEntries(
            Object.entries(element.options).map(([selector, option]) => [
              selector,
              { value: accentElements(option.value) },
            ]),
          ),
        }
      default:
        return element
    }
  })
}

/** Ünïcödé Tëst: the English catalog as accented look-alike text, wrapped and padded (ADR 0009). */
export function pseudoLocalize<Id extends string>(
  catalog: Record<Id, string>,
): Record<Id, MessageFormatElement[]> {
  const entries = (Object.entries(catalog) as [Id, string][]).map(
    ([id, message]): [Id, MessageFormatElement[]] => [
      id,
      [
        { type: TYPE.literal, value: '⏩ ' },
        ...accentElements(parse(message)),
        { type: TYPE.literal, value: ` ${padding(message)} ⏪` },
      ],
    ],
  )
  return Object.fromEntries(entries) as Record<Id, MessageFormatElement[]>
}
