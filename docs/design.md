# Design

Why: [ADR 0017](adr/0017-design-tokens-and-visual-check-loop.md). Read this before building or changing a screen.

## Look
Calm and friendly, in the manner of LinkedIn/Facebook structure with Nextdoor/Airbnb warmth: white cards on a soft warm-grey canvas, one teal brand colour, rounded corners, little decoration. Font is Geist. The wordmark is text plus a small icon in a primary-coloured tile.

## Tokens (`src/index.css`)
- Use only semantic tokens: `bg-background` (canvas), `bg-card`, `text-foreground`, `text-muted-foreground`, `bg-primary text-primary-foreground`, `bg-accent text-accent-foreground` (soft brand tint, pills and active tabs), `border`, `text-destructive`, `text-success`. Opacity modifiers are fine (`bg-primary/80`).
- Never use palette classes (`bg-blue-500`), `white`/`black`, arbitrary colours or hex. ESLint fails the build.
- A new colour need is a new token with a light and a dark value in `index.css`, not a one-off.
- Brand colour: change `--brand-hue` (192 teal; 30 coral).
- Dark mode is automatic from the system setting. Do not add `.dark` classes or a toggle. Use `dark:` variants only when a token cannot express it.

## Layout
- Mobile-first: write the small-screen classes unprefixed, then add `lg:` overrides. Shell, nav and column come from `AppShell` (`components/`); do not build a second one.
- One content column (`max-w-3xl`). Page title is `PageHeader`, rendered by the route. Content is `Card`s; lists are `ul` of cards or divided rows, not wide tables, so they work at 375px.
- Gaps and padding on the 4px scale: `gap-3`/`gap-4` between cards, `p-4` inside, `px-4` page gutter.
- Touch targets: primary buttons and inputs are `h-11` on mobile and `lg:h-8`/`lg:h-9` on desktop.
- Icons are lucide, decorative ones `aria-hidden`; an icon-only control has an `aria-label`.

## Long text
Every string must survive the padded `en-XA` Language (about +100% on short labels). Give text containers `min-w-0` and `[overflow-wrap:anywhere]`, let labels wrap rather than truncate, and never fix a width to the English text.

## Checking your work
1. `npm run design:screenshots` (in `src/frontend`). It writes `design-screenshots/<screen>/<language>-<scheme>-<width>.png` (gitignored) and fails on sideways scroll or content spilling out of its box. Add a screen by adding it to `SCREENS` in `e2e/design/screenshots.spec.ts`.
2. Open the images for the screens you touched: `en-US` and `en-XA`, light and dark, mobile and desktop. Look for clipped or overlapping text, low contrast, tabs or buttons that wrap badly, and anything that differs from its neighbours.
3. `scripts/check.sh all --fast` stays green.
