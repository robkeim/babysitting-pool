import { ESLint } from 'eslint'
import tseslint from 'typescript-eslint'
import { describe, expect, it } from 'vitest'
import { colorTokenConfigs } from '../../eslint.colors.js'

// Guards the raw-color ban (docs/design.md) so it cannot silently stop working.
const eslint = new ESLint({
  overrideConfigFile: true,
  overrideConfig: [{ languageOptions: { parser: tseslint.parser } }, ...colorTokenConfigs],
})

async function count(source: string, filePath = 'src/features/credits/a.tsx') {
  const [result] = await eslint.lintText(source, { filePath })
  return result?.messages.length ?? 0
}

describe('color tokens', () => {
  it.each([
    ['a palette class', '<p className="bg-blue-500" />'],
    ['a variant on a palette class', '<p className="dark:hover:text-red-400 p-2" />'],
    ['white', '<p className="text-white" />'],
    ['an arbitrary color', '<p className="bg-[#fff]" />'],
    ['a template literal', 'const c = `p-2 border-gray-200 ${x}`'],
    ['a hex literal', "const color = '#1a2b3c'"],
  ])('rejects %s', async (_name, source) => {
    expect(await count(source)).toBeGreaterThan(0)
  })

  it.each([
    ['theme tokens', '<p className="bg-card text-muted-foreground border-border bg-primary/80" />'],
    ['a token with a palette-like suffix', '<p className="text-primary-foreground" />'],
    ['an unrelated word', "const label = 'The text-white-space is fine'"],
  ])('allows %s', async (_name, source) => {
    expect(await count(source)).toBe(0)
  })
})
