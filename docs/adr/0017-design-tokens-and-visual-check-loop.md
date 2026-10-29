# The UI is themed by semantic tokens, follows the system colour scheme, and is checked by looking at screenshots

Every colour in the frontend comes from a semantic token defined in `src/index.css` (`bg-card`, `text-muted-foreground`, `bg-primary`), never from a palette class or a literal. Light and dark are two sets of values for the same tokens, so a screen that uses tokens is correct in both by construction. The brand colour is one hue variable (`--brand-hue`: 192 is teal, 30 would be coral), so a re-brand is a one-line change.

Dark mode follows `prefers-color-scheme` through a media query and a `dark` Tailwind variant bound to it. There is no toggle and no class set from JavaScript: a Member who changes their phone or browser setting sees it change, and there is nothing to store. A manual override (light, dark, system) is a later addition if asked for.

Layout is mobile-first and borrows the LinkedIn/Facebook shape: a sticky top bar with the wordmark and account, a bottom tab bar below `lg` and the same tabs in the top bar from `lg` up, and one centred content column on a soft canvas of white cards. It is one `nav` element restyled by breakpoint, not two, so tests and screen readers see one set of links. `lg` and not `md` because four tabs, the wordmark and the account controls do not fit in a 768px bar.

An agent checks a screen against the design with three layers:

1. **Lint.** `eslint.colors.js` bans palette classes, `white`/`black` utilities, arbitrary colours and hex literals in `src/` (a Vitest fixture keeps the rule honest). Tokens are the only way to get colour.
2. **Look at it.** `npm run design:screenshots` drives the real stack and saves a PNG of each key screen per Language (`en-US` and the padded `en-XA`, ADR 0009), colour scheme and width (375px and 1280px). The agent opens the images and judges them against `docs/design.md`. The script also fails on sideways page scroll and on content spilling out of its box, which are the commonest ways a long translation breaks a layout.
3. **Rules in one page.** `docs/design.md` holds the tokens, spacing, touch-target and component rules the images are judged by.

Pixel-diff baselines were rejected: they fail on every font or date change and say nothing about whether the change is bad. The screenshot script is outside the gate (it is slow and a judgement, not a pass/fail) and runs when a change touches how something looks.

**Amendment (2026-10-27, navigation overflow).** Eight tabs for an Admin did not fit the bar and broke label by letter. The bar now holds four Member tabs and, for Admins, a More tab holding the Admin pages: a bottom sheet below `lg`, a dropdown menu from `lg` up. It is still one `nav` element. The Language picker moved from the top bar to the Profile page to give the tabs room. The shell never splits a word, so `overflow-wrap:anywhere` is gone from navigation labels.

**Amendment (2026-10-29, vacation mode).** One alternative theme exists: `vacationMode`, an operator toggle (ADR 0016) that swaps the tokens for sand, coral and dusk values. The SPA sets `data-theme="vacation"` on `<html>` from the flag and remembers it in `localStorage` (a convenience that may be missing, so the page works without it) so a reload does not flash the default colours. The values live in `:root[data-theme='vacation']` blocks in `index.css`, with a light and a dark set, and no component knows about the theme. A new token needs a value there too. There is one theme and no Member-chosen picker.

Related: ADR 0003 (stack; shadcn stays vanilla except for tokens), ADR 0005 (`components/` holds shared composed components such as the shell), ADR 0009 (the test Language is the layout stress test).
