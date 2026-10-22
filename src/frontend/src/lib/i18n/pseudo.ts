import { parse, TYPE, type MessageFormatElement } from '@formatjs/icu-messageformat-parser'

const ACCENTS: Record<string, string> = {
  a: 'à',
  b: 'ƀ',
  c: 'ç',
  d: 'ð',
  e: 'é',
  f: 'ƒ',
  g: 'ĝ',
  h: 'ĥ',
  i: 'î',
  j: 'ĵ',
  k: 'ķ',
  l: 'ļ',
  m: 'ɱ',
  n: 'ñ',
  o: 'ö',
  p: 'þ',
  q: 'ǫ',
  r: 'ŕ',
  s: 'š',
  t: 'ţ',
  u: 'û',
  v: 'ṽ',
  w: 'ŵ',
  x: 'ẋ',
  y: 'ý',
  z: 'ž',
  A: 'À',
  B: 'Ɓ',
  C: 'Ç',
  D: 'Ð',
  E: 'É',
  F: 'Ƒ',
  G: 'Ĝ',
  H: 'Ĥ',
  I: 'Î',
  J: 'Ĵ',
  K: 'Ķ',
  L: 'Ļ',
  M: 'Ṁ',
  N: 'Ñ',
  O: 'Ö',
  P: 'Þ',
  Q: 'Ǫ',
  R: 'Ŕ',
  S: 'Š',
  T: 'Ţ',
  U: 'Û',
  V: 'Ṽ',
  W: 'Ŵ',
  X: 'Ẋ',
  Y: 'Ý',
  Z: 'Ž',
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
  return Array.from(text, (character) => ACCENTS[character] ?? character).join('')
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
