# i18n library landscape and pseudo-localization options

Research for issue #30 (feeds #31). Context: client-only Vite SPA, React, TypeScript; Language design is in the resolution of #17. Only first-party docs and repos were used. Fetched 2026-10-21; library docs move, so re-check versions before deciding.

## Summary

- Three mainstream candidates: **Lingui**, **react-intl (FormatJS)** and **react-i18next (i18next)**. The first two are ICU MessageFormat natively; i18next uses its own JSON plural/interpolation format by default and needs a plugin for ICU.
- **Lingui** covers the most of the wish list out of the box: ICU, extraction, a Vite plugin, typed message IDs (opt-in, with explicit IDs), a hard-coded-string lint rule, per-locale lazy-loaded catalogs, and a configurable pseudo-locale (`prepend`/`append`/`extend`/`extendCharacter`).
- **No existing web tool reproduces the Ünïcödé Tëst transform.** FormatJS `en-XA` and Lingui's pseudo-locale are deterministic but not seeded-random, have no length-tiered padding and no `⏩ ⏪` markers. Both parse ICU so they leave placeholders, plurals and tags alone, which is the hard part. A small custom function is the right call; see "Reusable pieces".
- **Padding tiers:** the starting tiers are directionally supported by a primary source, but the source (W3C citing IBM) gives larger and finer-grained numbers. See "Verifying the padding tiers".

## Comparison

| Concern | Lingui | react-intl / FormatJS | react-i18next / i18next |
|---|---|---|---|
| ICU plurals and placeholders | Native ICU MessageFormat, syntax validated at compile time ([intro](https://lingui.dev/introduction)) | ICU message syntax ([react-intl docs](https://formatjs.github.io/docs/react-intl/)) | Own format by default; ICU through `i18next-icu`, which i18next's type system recognises via `parseInterpolation: 'icu'` ([TypeScript docs](https://www.i18next.com/overview/typescript)) |
| Type-safe keys | Opt-in typed message IDs when using explicit IDs ([IDs guide](https://lingui.dev/guides/explicit-vs-generated-ids)). Default is generated IDs from the source text, so no keys to type | Augment `FormatjsIntl.Message` to narrow `id` (TypeScript 5.4+); manual list of keys ([docs](https://formatjs.github.io/docs/react-intl/)). Lint rule `enforce-message-types` generates argument types from ICU ([linter](https://formatjs.github.io/docs/tooling/linter/)) | Module augmentation of `CustomTypeOptions` from the default-language resource files; a selector API (`t($ => $.key)`) exists and is planned to become default in v26; large key sets can slow the compiler ([TypeScript docs](https://www.i18next.com/overview/typescript)) |
| Message extraction | `lingui extract` to PO by default, JSON and CSV also available ([intro](https://lingui.dev/introduction), [conf](https://lingui.dev/ref/conf)) | `formatjs extract` / `compile`, with a built-in formatters for Crowdin, Lokalise, Smartling, Transifex ([CLI](https://formatjs.github.io/docs/tooling/cli/)) | Not built in by the docs reviewed here; extraction is done with separate tools |
| Lint for hard-coded strings | `lingui/no-unlocalized-strings` matches all JSX text, string literals and template literals, then applies ignore lists; may need `useTsTypes` ([rule doc](https://github.com/lingui/eslint-plugin/blob/main/docs/rules/no-unlocalized-strings.md), [plugin page](https://lingui.dev/ref/eslint-plugin)) | `formatjs/no-literal-string-in-jsx` (also checks `aria-label`, `alt`, `placeholder`, `title`) and `no-literal-string-in-object` ([linter](https://formatjs.github.io/docs/tooling/linter/)) | Separate `eslint-plugin-i18next` `no-literal-string`; by default only plain JSX text ([repo](https://github.com/edvardchen/eslint-plugin-i18next)) |
| Lazy-loaded catalogs | Documented pattern: dynamic `import()` of one locale's catalog, then `i18n.load` / `i18n.activate`; each language is its own chunk ([guide](https://lingui.dev/guides/dynamic-loading-catalogs)). Vite plugin compiles catalogs on the fly | Not covered on the react-intl page; locale data and messages are plain objects you load yourself ([docs](https://formatjs.github.io/docs/react-intl/)) | `i18next-resources-to-backend` wraps a dynamic `import()` loader; `i18next-http-backend` for fetching ([docs](https://www.i18next.com/how-to/add-or-load-translations)) |
| Pseudo-locale | Built in and configurable (below) | `formatjs compile --ast --pseudo-locale` supports `xx-LS`, `xx-AC`, `xx-HA`, `en-XA`, `en-XB` ([CLI](https://formatjs.github.io/docs/tooling/cli/)) | No built-in; community plugin `i18next-pseudo` exists (not evaluated; no first-party source reached) |
| Runtime size | About 2 kB runtime, no message parser at runtime; messages compiled at build by a Babel or SWC plugin ([intro](https://lingui.dev/introduction)) | Not stated in the pages reviewed | Not stated in the pages reviewed |

Other libraries (Paraglide, Intlayer, etc.) were not investigated; the three above match the question's shortlist.

Notes relevant to this repo:
- Lingui's ESLint plugin docs describe Oxlint support via `jsPlugins` but say `useTsTypes` does not work under Oxlint. Commit 7dce843 says oxlint is being removed, so this is probably moot, but worth knowing if it stays.
- Lingui's default is generated IDs from source text; the #17 resolution wants codes mapped to catalog entries and a CI key-parity check. That works with explicit IDs (and then typed IDs), at the cost of Lingui's main ergonomic selling point. FormatJS and i18next are key-based natively.
- Lingui uses PO as the default catalog format and the pseudo-locale is generated at `compile`, not stored (see below); this fits "no catalog file of its own" from #17.

## Pseudo-localization details

### FormatJS

Source: [`packages/cli-lib/pseudo_locale.ts`](https://github.com/formatjs/formatjs/blob/main/packages/cli-lib/pseudo_locale.ts) and the [CLI docs](https://formatjs.github.io/docs/tooling/cli/).

- Operates on the parsed ICU AST and only rewrites literal elements (including those inside plural/select branches and tags), so placeholders and structure are untouched.
- `en-XA` maps each Latin letter through a fixed table (for example `m` to `ḿ`), doubles `a`, `e`, `o`, `u` "to emulate ~30% longer text", and wraps the message in `[` `]`. `en-XB` flips letters and adds RTL embedding marks (U+202E ... U+202C). `xx-LS` appends 25 `S` characters; `xx-AC` upper-cases; `xx-HA` prefixes `[javascript]`.
- Fully deterministic, a fixed map: no seed, no per-message randomness, no length-scaled padding. The expansion is a flat ~30% only for vowels.
- Documented output for `my name is {name}`: `en-XA` -> `[ḿẏ ƞȧȧḿḗḗ īş {name}]`.
- The transform functions are marked `@internal` in the source; the supported surface is the CLI.

### Lingui

Sources: [pseudolocalization guide](https://lingui.dev/guides/pseudolocalization), [config reference](https://lingui.dev/ref/conf), and [`pseudoLocalize.ts`](https://github.com/lingui/js-lingui/blob/main/packages/cli/src/api/pseudoLocalize.ts).

- Config `pseudoLocale` takes `{ locale, prepend, append, extend, extendCharacter, override, rightToLeft }` (the plain-string form is deprecated). `extend: 0.3` widens by 30%; `extendCharacter` defaults to a space; `override` replaces every non-token character with one character (finding untranslated text); `rightToLeft` uses Unicode RTL override marks.
- Generated during `compile` from the extracted messages, not stored by hand.
- Implementation wraps the third-party npm package `pseudolocale` (Lingui's own code only inserts delimiters around HTML tags, ICU macros and `{variables}` so they survive). The `pseudolocale` package's options (`prepend`, `append`, `extend`, `delimiter`) are passed straight through.
- `extend` is a single flat ratio per locale, not tiered by message length; the guide does not state whether output is deterministic and the source shows no seeding.

### Android `en-XA`

Source: [Android pseudolocales](https://developer.android.com/guide/topics/resources/pseudolocale). Accented Latin text, extra non-accented text to expand it, and brackets around each message unit to expose truncation. The page gives **no numeric expansion percentage**. It confirms the model (accents, brackets, expansion), not specific tiers.

## Reusable pieces vs custom

| Requirement from #17 | Existing tool? |
|---|---|
| Leave placeholders, plural/select, tags untouched | Yes in principle: both FormatJS and Lingui do it, FormatJS via `@formatjs/icu-messageformat-parser` AST |
| Accented look-alike substitution | Fixed maps exist (FormatJS `ACCENTED_MAP`), but deterministic one-to-one, not seeded random choice among several look-alikes |
| One seed per message, from the whole string | No |
| Length-scaled padding tiers | No (FormatJS: flat vowel doubling; Lingui: flat `extend` ratio) |
| `⏩ ... ⏪` markers | No (FormatJS uses `[ ]`; Lingui `prepend`/`append` could carry them) |
| Padding mix of tall/stacked/wide glyphs and spaces | No |
| Runs at runtime on the English catalog in the browser | FormatJS and Lingui run it at build/compile time via CLI |

Conclusion: a small custom function is warranted, roughly "parse the ICU message, rewrite literal runs, wrap once". The parsing piece can lean on whatever library #31 picks (`@formatjs/icu-messageformat-parser` is standalone and is what FormatJS uses; Lingui has its own token-delimiter approach). The substitution maps in FormatJS's file (itself based on Mozilla's L10nRegistry) are a useful reference for the accent tables but are not an exported API. Whichever library is chosen, the custom transform is independent of it because #17 defines it as a pure function of the English string, applied before the library's formatter.

Caveat: Lingui and i18next compile or interpolate differently, so for a runtime transform the integration point matters: with Lingui the English message is compiled at build time (the runtime has no parser, per the intro), so a runtime pure function over the source string needs either a build-time step or a runtime parser. This is a real input to #31.

## Verifying the padding tiers

Starting tiers in #17: under 10 characters +100%, 10-30 +50%, over 30 +35%.

Primary source: W3C Internationalization, [Text size in translation](https://www.w3.org/International/articles/article-text-size.en), citing IBM's "Guidelines to design global solutions" for English into European languages:

| English characters | Average expansion (as published) |
|---|---|
| up to 10 | 200-300% |
| 11-20 | 180-200% |
| 21-30 | 160-180% |
| 31-50 | 140-160% |
| 51-70 | 151-170% (printed that way; out of sequence, probably a typo) |
| over 70 | 130% |

Reading: the article does not define the percentage, but its own example ("The 300% expansion from English to Italian") shows that "200%" means the translation is twice as long as the English, i.e. +100% padding. That is an inference from the article, not an explicit definition.

Mapping to the proposed tiers (padding = published figure minus 100%):

| #17 tier | Proposed padding | Source implies | Verdict |
|---|---|---|---|
| under 10 chars | +100% | +100% to +200% | Lower bound only; the source's average is +100-200% |
| 10-30 chars | +50% | +60% to +100% (11-20: +80-100%; 21-30: +60-80%) | Low |
| over 30 chars | +35% | +30% to +60% for 31-70, +30% over 70 | Reasonable for long text, low for 31-50 |

Notes: these are averages for European languages, not worst cases; the source says nothing about the Latin-script-specific figure, and nothing about non-Latin or CJK. Recommended adjustment if the intent is a stress test rather than an average: use the upper end of the source (about +200%, +100%, +60%, +30%) or add a tier at 31-50 chars. FormatJS's flat ~30% (`en-XA`) is consistent with the long-string end only.

## Open questions for #31

1. Lingui's generated-ID default vs the code-to-catalog-entry model in #17 (explicit IDs needed).
2. Where the runtime Ünïcödé Tëst transform plugs in for a library that compiles messages at build time.
3. Whether to keep Oxlint, which affects the Lingui lint option `useTsTypes`.
4. i18next-pseudo and other community plugins were not evaluated (no first-party source reached).
