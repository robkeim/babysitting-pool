// Colour comes from the theme tokens in src/index.css, never from a palette class or a literal, so light and
// dark stay consistent by construction (docs/design.md). `src/components/ui` is shadcn output and is not linted.
const PALETTE =
  'slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose'
const UTILITIES =
  'bg|text|border|ring|outline|fill|stroke|from|via|to|divide|shadow|decoration|accent|caret|placeholder'

export const RAW_COLOR = new RegExp(
  `(^|[^a-z0-9-])(${UTILITIES})-((${PALETTE})-[0-9]{2,3}|white|black)($|[^a-z0-9-])|(^|[^a-z0-9-])(${UTILITIES})-\\[(#|rgb|hsl|oklch|color)`,
)
const HEX_LITERAL = /^#[0-9a-fA-F]{3,8}$/

const message =
  'Use a theme token (bg-card, text-muted-foreground, bg-primary, ...) instead of a raw colour (docs/design.md).'

/** @type {import('eslint').Linter.Config[]} */
export const colorTokenConfigs = [
  {
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['**/*.test.{ts,tsx}', 'src/test/**', 'src/components/ui/**'],
    rules: {
      'no-restricted-syntax': [
        'error',
        { selector: `Literal[value=${RAW_COLOR.toString()}]`, message },
        { selector: `TemplateElement[value.raw=${RAW_COLOR.toString()}]`, message },
        { selector: `Literal[value=${HEX_LITERAL.toString()}]`, message },
      ],
    },
  },
]
